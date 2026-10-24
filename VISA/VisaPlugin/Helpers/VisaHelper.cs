using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Ivi.Visa;
using System.Diagnostics;

namespace VISA.Helpers;

/// <summary>
/// VISA 通用辅助方法
/// </summary>
public static class VisaHelper
{
    private static readonly ConditionalWeakTable<IMessageBasedSession, SemaphoreSlim> SessionLocks = new();
    private sealed class RegistrationState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public long CloseGeneration;
    }
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, RegistrationState>> RegistrationLocks = new();
    private static RegistrationState Registration(object registry, string key) =>
        RegistrationLocks.GetValue(registry, _ => new()).GetOrAdd(key, _ => new());

    public static SemaphoreSlim GetRegistrationLock(object registry, string key) =>
        Registration(registry, key).Gate;
    public static long GetCloseGeneration(object registry, string key) => Interlocked.Read(ref Registration(registry, key).CloseGeneration);
    public static void MarkCloseRequested(object registry, string key) => Interlocked.Increment(ref Registration(registry, key).CloseGeneration);
    public static void ThrowIfOpenSuperseded(object registry, string key, long generation)
    {
        if (GetCloseGeneration(registry, key) != generation)
            throw new InvalidOperationException("VISA 建立期间收到同名关闭请求，旧代次不得迟到报告可用");
    }
    public static int RemainingBudget(long startedAt, int timeoutMs)
    {
        if (timeoutMs == Timeout.Infinite) return Timeout.Infinite;
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        double remaining = timeoutMs - Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException($"VISA 建立/关闭事务总期限 {timeoutMs} ms 已到");
        return (int)Math.Ceiling(remaining);
    }

    public static async Task WaitForRegistrationAsync(SemaphoreSlim gate, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "登记门必须有正数期限，即使原生打开配置为无限等待");
        if (!await gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false))
            throw new TimeoutException($"VISA 等待同名会话建立/关闭事务超过 {timeoutMs} ms，不能报告已关闭");
    }

    /// <summary>获取会话对应的 I/O 互斥锁，用于串行化并行步骤对同一会话的访问（会话销毁后锁自动回收）</summary>
    public static SemaphoreSlim GetLock(IMessageBasedSession session) =>
        SessionLocks.GetValue(session, _ => new SemaphoreSlim(1, 1));

    /// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
    public static string GetSessionKey(string connectionName) => $"__VISA_{connectionName}";

    public static string GetPhysicalResourceKey(string resourceString)
    {
        var name = resourceString.Trim().ToUpperInvariant();
        if (name.Length == 0) throw new ArgumentException("VISA 物理资源不能为空");
        foreach (string bus in new[] { "TCPIP", "GPIB", "USB", "PXI", "VXI" })
            if (name.StartsWith(bus + "::", StringComparison.Ordinal)) { name = bus + "0" + name[bus.Length..]; break; }
        return "VISA:" + name;
    }

    /// <summary>根据连接名称生成终止符存储的唯一键</summary>
    public static string GetTerminatorKey(string connectionName) => $"__VISA_{connectionName}__TERM";

    /// <summary>
    /// 将用户配置的终止符归一化为真实字符（同时支持转义文本 \n、\r、\r\n 与真实字符），为空时默认换行符
    /// </summary>
    public static string NormalizeTerminator(string? terminator)
    {
        if (string.IsNullOrEmpty(terminator))
            return "\n";
        return terminator.Replace("\\r", "\r").Replace("\\n", "\n");
    }

    /// <summary>
    /// 打开 VISA 会话
    /// </summary>
    [Obsolete("请使用 VisaSessionResource.InitializeAsync(RawOpenSession, ConfigureSession)，原生句柄必须先归 owner")]
    public static IMessageBasedSession OpenSession(string resourceString, int openTimeoutMs, int ioTimeoutMs, string terminator) =>
        throw new NotSupportedException("无 owner 的 VISA OpenSession 已停用，避免初始化失败/Dispose 失败后丢失句柄；请使用 VisaSessionResource.InitializeAsync 先接管 RawOpenSession 返回值，再配置会话");

    /// <summary>只取得原始句柄；调用方必须立即交由 VisaSessionResource 接管。</summary>
    public static IDisposable RawOpenSession(string resourceString, int openTimeoutMs) =>
        GlobalResourceManager.Open(resourceString, AccessModes.None, openTimeoutMs);

    /// <summary>必须在 owner 已接管的事务中配置；失败由 owner 保留句柄并负责可重试关闭。</summary>
    public static void ConfigureSession(IMessageBasedSession session, int ioTimeoutMs, string terminator, Action? submissionGuard = null)
    {
        if (ioTimeoutMs == 0 || ioTimeoutMs < Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(ioTimeoutMs));
        var termChar = NormalizeTerminator(terminator)[^1];
        if (termChar > 0xFF) throw new InvalidOperationException("终止符必须是单字节字符（如 \\n、\\r\\n），不支持中文等多字节字符");
        submissionGuard?.Invoke(); session.TimeoutMilliseconds = ioTimeoutMs;
        submissionGuard?.Invoke(); session.TerminationCharacter = (byte)termChar;
        submissionGuard?.Invoke(); session.TerminationCharacterEnabled = true;
    }

    public static void ValidateOpcResponse(string? response)
    {
        if (!string.Equals(response?.Trim(), "1", StringComparison.Ordinal))
            throw new InvalidOperationException($"VISA *OPC? 响应不是完成标志 1，实际响应=[{response}]，不能判定仪器已完成");
    }

    /// <summary>必须在同一个真实 I/O 事务内调用，恢复失败不能覆盖原始通信异常。</summary>
    public static T WithTemporaryTimeout<T>(IMessageBasedSession session, int timeoutMs, Func<T> action)
    {
        if (timeoutMs <= 0) return action();
        var original = session.TimeoutMilliseconds;
        Exception? primary = null;
        try
        {
            session.TimeoutMilliseconds = timeoutMs;
            return action();
        }
        catch (Exception ex) { primary = ex; throw; }
        finally
        {
            try { session.TimeoutMilliseconds = original; }
            catch (Exception cleanup)
            {
                if (primary != null) throw new AggregateException("VISA I/O 与超时设置恢复均失败", primary, cleanup);
                throw new InvalidOperationException("VISA I/O 已退出，但恢复会话超时失败，会话不可继续复用", cleanup);
            }
        }
    }

    /// <summary>
    /// 发送 SCPI 命令（追加配置的终止符），并刷新写缓冲区确保数据实际发出（END/EOI 结尾）
    /// </summary>
    public static void Write(IMessageBasedSession session, string command, string terminator = "\n", Action? submissionGuard = null)
    {
        submissionGuard?.Invoke();
        session.FormattedIO.Write(command + terminator);
        submissionGuard?.Invoke();
        session.FormattedIO.FlushWrite(true);
    }

    /// <summary>
    /// 读取响应
    /// </summary>
    public static string Read(IMessageBasedSession session, bool trim)
    {
        var response = session.FormattedIO.ReadLine();
        return trim ? response.Trim() : response;
    }

    /// <summary>
    /// 发送命令并读取响应（Query）
    /// </summary>
    public static string Query(IMessageBasedSession session, string command, bool trim, string terminator = "\n", Action? submissionGuard = null)
    {
        Write(session, command, terminator, submissionGuard);
        submissionGuard?.Invoke();
        return Read(session, trim);
    }

    /// <summary>
    /// 兼容的独立计算软等待辅助函数；不拥有设备句柄，不能用于会话 I/O 或资源释放。
    /// 当前所有 VISA 步骤使用 VisaSessionResource 跟踪实际调用及隔离收尾。
    /// 此函数取消/超时仅结束调用者等待，不能证明 action 已退出。
    /// </summary>
    public static async Task<T> RunWithTimeoutAsync<T>(Func<T> action, int timeoutMs, string operation, CancellationToken cancellationToken)
    {
        var task = Task.Run(action, CancellationToken.None);
        try
        {
            return await task.WaitAsync(TimeSpan.FromMilliseconds(GetSoftTimeoutMs(timeoutMs)), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"VISA {operation} 超时（{timeoutMs} ms），设备未在规定时间内响应");
        }
    }

    /// <summary>
    /// 在后台线程执行同步 VISA I/O（无返回值），并施加软超时兜底
    /// </summary>
    public static async Task RunWithTimeoutAsync(Action action, int timeoutMs, string operation, CancellationToken cancellationToken)
    {
        await RunWithTimeoutAsync(() => { action(); return true; }, timeoutMs, operation, cancellationToken);
    }

    /// <summary>获取会话的 I/O 超时（毫秒），读取失败时回退到默认值</summary>
    public static int GetIoTimeoutMs(IMessageBasedSession session, int fallbackMs = 5000)
    {
        try
        {
            var timeout = session.TimeoutMilliseconds;
            return timeout > 0 ? timeout : fallbackMs;
        }
        catch
        {
            return fallbackMs;
        }
    }

    /// <summary>软超时在驱动超时基础上留出余量，优先让 VISA 自身抛出更精确的超时异常</summary>
    public static int GetSoftTimeoutMs(int timeoutMs)
    {
        if (timeoutMs == Timeout.Infinite) return Timeout.Infinite;
        if (timeoutMs <= 0) return 5000;
        var soft = timeoutMs + 1000;
        return soft < timeoutMs ? int.MaxValue : soft;
    }
}
