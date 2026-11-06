using System.Diagnostics;
using System.Text;
using LabVIEWCallPlugin.BridgeProtocol;
using LabVIEWCallPlugin.LVadapter;

namespace LabVIEWCallPlugin.Execution;

/// <summary>One serial native host. A cancelled waiter never releases an outstanding native request.</summary>
internal sealed class IsolatedViBridge : IDisposable
{
    internal static readonly IsolatedViBridge Shared = new(CreateStartInfo,
        (port, token) => ViServerProbe.CaptureIdentity(port, token));
    private readonly Func<ProcessStartInfo> _startInfo;
    private readonly Func<int, CancellationToken, ViServerIdentity>? _captureServer;
    private readonly Dictionary<int, ViServerIdentity> _serverIdentities = [];
    private ViServerIdentity? _serverIdentity;
    private readonly object _gate = new();
    private Process? _process;
    private Stream? _input;
    private Stream? _output;
    private string? _failure;
    private string _phase = "启动辅助进程";
    private long _nextId;
    private int _disposeRequested;
    private readonly Queue<string> _diagnostics = new();

    internal IsolatedViBridge(Func<ProcessStartInfo> startInfo,
        Func<int, CancellationToken, ViServerIdentity>? captureServer = null)
        => (_startInfo, _captureServer) = (startInfo, captureServer);

    internal string TargetDiagnostic => Volatile.Read(ref _serverIdentity)?.Diagnostic ?? "VI Server 尚未连接";

    private void CheckServer(int port, CancellationToken ct)
    {
        if (_captureServer == null) return;
        var current = _captureServer(port, ct);
        current.Validate();
        if (current.Port != port)
            throw new InvalidOperationException($"VI Server 身份端口与请求 {port} 不符，未接受该实例。");
        if (_serverIdentities.TryGetValue(port, out var pinned)) pinned.EnsureSame(current);
        else _serverIdentities.Add(port, current);
        // Switching endpoints must not replace a prior port's process-generation pin.
        Volatile.Write(ref _serverIdentity, pinned ?? current);
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        var directory = Path.GetDirectoryName(typeof(IsolatedViBridge).Assembly.Location)
            ?? throw new InvalidOperationException("无法确定 LabVIEW 插件目录。");
        var host = Path.Combine(directory, "bridge", "LabVIEWCall.BridgeHost.exe");
        if (!File.Exists(host))
            throw new FileNotFoundException("缺少 LabVIEW 独立桥接程序，请部署完整插件文件夹。", host);
        return new ProcessStartInfo(host) { WorkingDirectory = Path.GetDirectoryName(host)! };
    }

    internal ConnectPanelInfo ReadPanel(string path, string inputs, string outputs, CancellationToken ct, int port = 3363)
    {
        path = NormalizeViPath(path);
        Send(new() { Operation = BridgeOperation.Connect, Port = port }, ct);
        // Fetch clean metadata: the native merge can discard canonical array bindings.
        // Preserve user configuration atomically in managed code after normalization.
        var live = ViNativeMetadata.Normalize(Send(new() { Operation = BridgeOperation.Panel, Path = path, Port = port }, ct).Panel
            ?? throw new InvalidDataException("辅助进程没有返回 VI 连接器信息。"));
        return ViConnectorBindings.Preserve(live, inputs, outputs);
    }

    internal ConnectPanelInfo GetMetadata(string path, CancellationToken ct, Action checkActive, int port = 3363)
    {
        path = NormalizeViPath(path);
        checkActive();
        Send(new() { Operation = BridgeOperation.Connect, Port = port }, ct, checkActive);
        return ViNativeMetadata.Normalize(Send(new() { Operation = BridgeOperation.Metadata, Path = path, Port = port }, ct, checkActive).Panel
            ?? throw new InvalidDataException("辅助进程没有返回 VI 端子信息。"));
    }

    internal void Edit(string path, CancellationToken ct, int port = 3363)
    {
        path = NormalizeViPath(path);
        Send(new() { Operation = BridgeOperation.Connect, Port = port }, ct);
        Send(new() { Operation = BridgeOperation.Edit, Path = path, Port = port }, ct);
    }

    internal uint Load(string path, CancellationToken ct, Action checkActive, int port = 3363) =>
        Send(new() { Operation = BridgeOperation.Load, Path = NormalizeViPath(path), Port = port }, ct, checkActive).Reference;

    internal static string NormalizeViPath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".vi", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请提供 .vi 文件的完整路径。", nameof(path));
        // LabVIEW's native String To Path does not interpret mixed Windows separators
        // identically to .NET File.Exists; normalize before crossing that boundary.
        return Path.GetFullPath(path);
    }

    internal (short[] Type, byte[] Data) Get(uint reference, string parameter, CancellationToken ct, Action checkActive, int port = 3363)
    {
        var result = Send(new() { Operation = BridgeOperation.Get, Reference = reference, ParameterName = parameter, Port = port }, ct, checkActive);
        return (result.Type, result.Data);
    }

    internal void Set(uint reference, string parameter, short[] type, byte[] data, CancellationToken ct, Action checkActive, int port = 3363) =>
        Send(new() { Operation = BridgeOperation.Set, Reference = reference, ParameterName = parameter,
            Type = type, Data = data, Port = port }, ct, checkActive);

    internal uint Run(uint reference, bool show, bool close, CancellationToken ct, Action checkActive, int port = 3363) =>
        Send(new() { Operation = BridgeOperation.Run, Reference = reference, ShowPanel = show, ClosePanel = close, Port = port }, ct, checkActive).Reference;

    internal BridgeResponse Send(BridgeRequest request, CancellationToken ct, Action? checkActive = null)
    {
        lock (_gate)
        {
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
                if (_failure != null) throw new InvalidOperationException(_failure);
                _ = request.ValidatePort();
                ct.ThrowIfCancellationRequested();
                checkActive?.Invoke();
                try { EnsureStarted(); }
                catch (Exception error) { throw RecordFailure(error); }
                ct.ThrowIfCancellationRequested();
                checkActive?.Invoke();
                request.Id = ++_nextId;
                _phase = request.Operation.ToString();
                // A successful write admits the request. Keep reading until it actually returns,
                // even when the step has already stopped waiting through ViCallGate.
                BridgeResponse response;
                bool submitted = false;
                try
                {
                    BridgeWire.Write(_input!, request, () =>
                    {
                        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
                        ct.ThrowIfCancellationRequested();
                        checkActive?.Invoke();
                        CheckServer(request.Port, ct);
                        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
                        ct.ThrowIfCancellationRequested();
                        checkActive?.Invoke();
                        submitted = true;
                    });
                    do
                    {
                        response = BridgeWire.Read<BridgeResponse>(_output!)
                            ?? throw new EndOfStreamException("辅助进程在返回结果前关闭了通信管道。");
                        if (response.Id != request.Id || response.ProcessId != _process!.Id)
                            throw new InvalidDataException("辅助进程返回的调用标识或进程标识不匹配。");
                        if (response.Kind == "Phase") _phase = response.Phase;
                    } while (response.Kind == "Phase");
                    if (response.Kind != "Result") throw new InvalidDataException("辅助进程返回了未知消息类型。");
                }
                catch (Exception error) when (submitted) { throw RecordFailure(error); }
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
                ct.ThrowIfCancellationRequested();
                checkActive?.Invoke();
                try { CheckServer(request.Port, ct); }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    throw new InvalidOperationException("原生请求已提交并收到响应，但返回后身份复核失败，本次结果不被接受，不自动重试。"
                        + $"原始错误：{error.Message}", error);
                }
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
                ct.ThrowIfCancellationRequested();
                checkActive?.Invoke();
                if (!string.IsNullOrEmpty(response.Error))
                    throw new InvalidOperationException($"LabVIEW 辅助进程调用失败，阶段={_phase}，PID={_process!.Id}；{TargetDiagnostic}。{response.Error}");
                return response;
            }
            finally
            {
                if (Volatile.Read(ref _disposeRequested) != 0) CloseHandles();
            }
        }
    }

    private InvalidOperationException RecordFailure(Exception error)
    {
        string exit = "尚未确认退出";
        if (_process is { } process && process.WaitForExit(100))
            exit = $"0x{unchecked((uint)process.ExitCode):X8}";
        string diagnostics;
        lock (_diagnostics) diagnostics = string.Join(Environment.NewLine, _diagnostics);
        _failure = $"LabVIEW 辅助进程通信失败，阶段={_phase}，PID={_process?.Id}，退出码={exit}。"
            + "本次调用结果未知，已阻止后续原生请求；请确认目标 VI 状态后重启平台。"
            + $" 原始错误：{error.Message} {diagnostics}";
        return new InvalidOperationException(_failure, error);
    }

    private void EnsureStarted()
    {
        if (_process != null) return;
        var start = _startInfo();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.StandardErrorEncoding = Encoding.UTF8;
        _process = Process.Start(start) ?? throw new InvalidOperationException("未能启动 LabVIEW 辅助进程。");
        _input = _process.StandardInput.BaseStream;
        _output = _process.StandardOutput.BaseStream;
        _process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not { Length: > 0 } line) return;
            lock (_diagnostics)
            {
                if (_diagnostics.Count == 16) _diagnostics.Dequeue();
                _diagnostics.Enqueue(line[..Math.Min(line.Length, 2048)]);
            }
        };
        _process.BeginErrorReadLine();
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        var ready = BridgeWire.Read<BridgeResponse>(_output);
        if (ready is not { Kind: "Ready", Id: 0 } || ready.ProcessId != _process.Id)
            throw new InvalidDataException("LabVIEW 辅助进程初始化握手失败。");
    }

    private void OnProcessExit(object? sender, EventArgs args)
    {
        // Closing stdin asks the owned host to exit after its current request.
        // It does not abort or terminate the user's LabVIEW instance.
        try { _input?.Dispose(); }
        catch (IOException error) { Trace.TraceWarning($"LabVIEW 辅助进程退出通知失败：{error.Message}"); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        // An in-flight native call may never return. Its owner closes the pipes in
        // Send's finally; disposing from a UI or shutdown thread must not wait for it.
        if (!Monitor.TryEnter(_gate)) return;
        try { CloseHandles(); }
        finally { Monitor.Exit(_gate); }
    }

    private void CloseHandles()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _input?.Dispose();
        _output?.Dispose();
        _process?.Dispose();
        _input = null;
        _output = null;
        _process = null;
    }
}
