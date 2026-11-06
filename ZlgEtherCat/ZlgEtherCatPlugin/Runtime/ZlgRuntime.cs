using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Runtime;

public static class ZlgRuntime
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, object> Reservations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MaintenanceBoards = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SdkWorker Discovery = new("枚举");
    private const string ResourceKey = "ZlgEtherCat.Runtime";
    private static readonly IZlgNativeApi Api = new ZlgNativeApi();
    private sealed class Scope(IExecutionContext context, IResourceRegistry registry) : IDisposable
    {
        internal readonly Dictionary<string, ZlgChannel> Channels = new(StringComparer.Ordinal);
        internal readonly SemaphoreSlim Admission = new(1, 1);
        internal bool Retired;
        internal void Check() { lock (Sync) if (Retired || !ReferenceEquals(context.Resources, registry) || !registry.TryGet<Scope>(ResourceKey, out var current) || !ReferenceEquals(current, this)) throw new InvalidOperationException("ZLG 执行资源代次已经退出"); }
        public void Dispose()
        {
            ZlgChannel[] channels;
            lock (Sync) { if (Retired) return; Retired = true; channels = Channels.Values.ToArray(); }
            foreach (var channel in channels) channel.Dispose();
        }
    }

    public static async Task<object?> ExecuteAsync(string operation, ZlgRequest request, IExecutionContext context, CancellationToken ct)
    {
        using var totalDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        totalDeadline.CancelAfter(request.TimeoutMs);
        try { return await ExecuteCoreAsync(operation, request, context, totalDeadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && totalDeadline.IsCancellationRequested)
        { throw new TimeoutException($"ZLG 步骤超过总期限 {request.TimeoutMs} ms", ex); }
    }

    private static async Task<object?> ExecuteCoreAsync(string operation, ZlgRequest request, IExecutionContext context, CancellationToken ct)
    {
        Scope scope;
        var registry = context.Resources;
        lock (Sync)
        {
            if (!registry.TryGet(ResourceKey, out scope!))
            {
                scope = new(context, registry);
                if (!registry.TryAdd(ResourceKey, scope, ResourceLifetime.Execution)) throw new InvalidOperationException("ZLG 执行资源登记失败");
            }
            scope.Check();
        }
        if (operation == "EnumerateBoards") return await Discovery.InvokeAsync(() => Enumerate(Api), request.TimeoutMs, ct).ConfigureAwait(false);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConnectionName);
        if (operation == "Open")
        {
            if (!await scope.Admission.WaitAsync(request.TimeoutMs, ct).ConfigureAwait(false)) throw new TimeoutException("打开通道排队超时");
            try
            {
                scope.Check();
                lock (Sync) if (scope.Channels.ContainsKey(request.ConnectionName)) throw new InvalidOperationException("连接名已存在或正在关闭");
                string selector = request.Get<string>("BoardSelector"); uint channel = request.Get<uint>("Channel");
                var boards = await Discovery.InvokeAsync(() => Enumerate(Api), request.TimeoutMs, ct).ConfigureAwait(false);
                var selected = SelectBoard(boards, selector);
                if (channel >= selected.Info.ChannelCount) throw new ArgumentException("板卡通道号超出范围");
                uint queueCapacity = await Discovery.InvokeAsync(() => Api.GetQueueCapacity(selected.Index, channel), request.TimeoutMs, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(selected.Info.SerialNumber)) throw new InvalidOperationException("SDK 未返回板卡序列号，不能建立稳定的物理通道归属");
                string physical = selected.Info.SerialNumber + ":" + channel;
                var owner = new object();
                lock (Sync)
                {
                    scope.Check();
                    if (MaintenanceBoards.Contains(selected.Info.SerialNumber)) throw new InvalidOperationException("板卡正在执行维护，禁止打开其他通道");
                    if (!Reservations.TryAdd(physical, owner)) throw new InvalidOperationException("该物理通道仍被占用或旧调用尚未真实退出");
                }
                var connection = new ZlgChannel(Api, request.ConnectionName,
                    () => { lock (Sync) if (Reservations.TryGetValue(physical, out var current) && ReferenceEquals(current, owner)) Reservations.Remove(physical); },
                    ex => context.Log(LogLevel.Error, "ZLG EtherCAT 后台或退出失败；资源保持隔离", ex.ToString()),
                    () => BeginMaintenance(selected.Info.SerialNumber), queueCapacity,
                    warning: notice => { scope.Check(); context.Log(LogLevel.Warn, notice.Message, System.Text.Json.JsonSerializer.Serialize(notice.Status)); });
                PluginCommon.ExecutionScopedExecutor.Own(connection.Dispose);
                lock (Sync)
                {
                    try { scope.Check(); scope.Channels.Add(request.ConnectionName, connection); }
                    catch { connection.Dispose(); throw; }
                }
                try
                {
                    // Serial selection prevents aliases referring to the same physical channel from bypassing ownership.
                    await connection.OpenAsync("NO" + selected.Info.SerialNumber, channel, request.TimeoutMs, ct).ConfigureAwait(false);
                    scope.Check(); return selected;
                }
                catch { connection.Dispose(); throw; }
            }
            finally { scope.Admission.Release(); }
        }
        ZlgChannel existing;
        lock (Sync) { scope.Check(); if (!scope.Channels.TryGetValue(request.ConnectionName, out existing!)) throw new InvalidOperationException("连接不存在，请先打开指定板卡通道"); }
        var result = await existing.ExecuteAsync(operation, request, context, ct).ConfigureAwait(false);
        if (operation == "Close") lock (Sync) if (scope.Channels.TryGetValue(request.ConnectionName, out var current) && ReferenceEquals(current, existing)) scope.Channels.Remove(request.ConnectionName);
        scope.Check(); return result;
    }

    private static IDisposable BeginMaintenance(string serial)
    {
        lock (Sync)
        {
            if (Reservations.Keys.Count(k => k.StartsWith(serial + ":", StringComparison.OrdinalIgnoreCase)) != 1 || !MaintenanceBoards.Add(serial))
                throw new InvalidOperationException("板卡维护要求本进程仅打开此板卡的一个通道，且没有其他维护任务");
            return new MaintenanceLease(serial);
        }
    }
    private sealed class MaintenanceLease(string serial) : IDisposable
    { public void Dispose() { lock (Sync) MaintenanceBoards.Remove(serial); } }

    internal sealed record BoardEntry(uint Index, BoardDetails Info);
    private static BoardEntry[] Enumerate(IZlgNativeApi api)
    {
        var driver = api.GetDriverInfo(); if (driver.BoardCount > 256) throw new InvalidOperationException("板卡数量超过允许范围");
        return Enumerable.Range(0, (int)driver.BoardCount).Select(i => new BoardEntry((uint)i, api.EnumerateBoard((uint)i))).ToArray();
    }
    internal static BoardEntry SelectBoard(BoardEntry[] boards, string selector)
    {
        IEnumerable<BoardEntry> matches;
        if (selector.StartsWith("ID", StringComparison.OrdinalIgnoreCase) && uint.TryParse(selector.AsSpan(2), out uint index)) matches = boards.Where(b => b.Index == index);
        else if (selector.StartsWith("AL", StringComparison.OrdinalIgnoreCase) && uint.TryParse(selector.AsSpan(2), out uint alias)) matches = boards.Where(b => b.Info.Alias == alias);
        else if (selector.StartsWith("NO", StringComparison.OrdinalIgnoreCase)) matches = boards.Where(b => string.Equals(b.Info.SerialNumber, selector[2..], StringComparison.OrdinalIgnoreCase));
        else throw new ArgumentException("板卡标识必须是 ID索引、AL别名或NO序列号");
        var selected = matches.ToArray();
        return selected.Length == 1 ? selected[0] : throw new ArgumentException("板卡标识不存在或不唯一");
    }
}
