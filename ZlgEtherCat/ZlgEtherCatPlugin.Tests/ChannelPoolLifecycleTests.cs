using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using xTestPlatform.Core.Engine;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class ChannelPoolLifecycleTests
{
    [Fact]
    public async Task IsolatedChannelCloseWaitsForTheActualNativeCallThenReleasesOwnership()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var api = new ChannelProbe { OnBusStatus = () => { entered.Set(); release.Wait(); } };
        var context = DispatchProxy.Create<IExecutionContext, ChannelContextProxy>();
        bool released = false;
        using var channel = new ZlgChannel(api, "test", () => released = true, _ => { });
        await channel.OpenAsync("NO1", 0, 1000, default);
        var read = channel.ExecuteAsync("ReadMasterStatus", Request() with { TimeoutMs = 100 }, context, default);
        Assert.True(entered.Wait(2000));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => read);
            var close = channel.ExecuteAsync("Close", Request(), context, default);
            Assert.False(close.IsCompleted);
            Assert.False(released);
            release.Set();
            await close.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(released);
            Assert.Equal(1, api.CloseCalls);
        }
        finally { release.Set(); await channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Fact]
    public async Task ReusingTheSameRegistryObjectCannotPublishAnOldPoolValue()
    {
        var api = new ChannelProbe();
        var context = DispatchProxy.Create<IExecutionContext, ChannelContextProxy>();
        var state = (ChannelContextProxy)(object)context;
        using var channel = new ZlgChannel(api, "test", () => { }, _ => { });
        await Start(channel, context);
        try
        {
            await channel.ExecuteAsync("BackgroundPoolCreate", Request(("PoolName", "Pool1"), ("MaxItems", 10)), context, default);
            await channel.ExecuteAsync("BackgroundPoolAddItem", Item(), context, default);
            api.Frames.Enqueue([10]);
            await Eventually(() => state.WriteCount == 1);
            var originalRegistry = state.Registry;
            originalRegistry.Items.Clear(); // Execution cleanup reuses the same registry instance.
            api.Frames.Enqueue([20]);
            await Eventually(async () =>
            {
                var status = JsonSerializer.SerializeToElement(await channel.ExecuteAsync("BackgroundPoolStatus", Request(("PoolName", "Pool1")), context, default));
                return !status.GetProperty("Items")[0].GetProperty("Enabled").GetBoolean();
            });
            Assert.Same(originalRegistry, state.Registry);
            Assert.Equal(1, state.WriteCount);
        }
        finally { await channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Fact]
    public async Task ConfiguredPoolsBlockAcquisitionStopAndItemChangesReuseWorkers()
    {
        var api = new ChannelProbe();
        var context = DispatchProxy.Create<IExecutionContext, ChannelContextProxy>();
        using var channel = new ZlgChannel(api, "test", () => { }, _ => { });
        await Start(channel, context);
        try
        {
            await channel.ExecuteAsync("BackgroundPoolCreate", Request(("PoolName", "Pool1"), ("MaxItems", 10)), context, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => channel.ExecuteAsync("StopAcquisition", Request(), context, default));
            await channel.ExecuteAsync("BackgroundPoolAddItem", Item(), context, default);
            await channel.ExecuteAsync("BackgroundPoolUpdateItem", Item(), context, default);
            await channel.ExecuteAsync("BackgroundPoolStopItem", Request(("PoolName", "Pool1"), ("ItemName", "Input1")), context, default);
            var status = JsonSerializer.SerializeToElement(await channel.ExecuteAsync("BackgroundPoolStatus", Request(("PoolName", "Pool1")), context, default));
            Assert.Equal(1, status.GetProperty("WorkerStarts").GetInt32());
            Assert.Equal(0, status.GetProperty("Items").GetArrayLength());
            await channel.ExecuteAsync("BackgroundPoolClose", Request(("PoolName", "Pool1")), context, default);
            await channel.ExecuteAsync("StopAcquisition", Request(), context, default);
        }
        finally { await channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    private static ZlgRequest Item() => Request(("PoolName", "Pool1"), ("ItemName", "Input1"),
        ("SlaveIndex", 0), ("Offset", 0), ("Length", 1), ("DataType", PdoDataType.UInt8),
        ("DataVariable", "Locals.Sample"), ("MaxAgeMs", 1000));

    private static ZlgRequest Request(params (string Name, object Value)[] values) =>
        new("test", 2000, "", values.ToDictionary(v => v.Name, v => (object?)v.Value));

    private static async Task Start(ZlgChannel channel, IExecutionContext context)
    {
        await channel.OpenAsync("NO1", 0, 1000, default);
        string file = Path.Combine(Path.GetTempPath(), "zlg-test-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(file, "<EtherCATConfig><Config><Cyclic><CycleTime>500</CycleTime></Cyclic><Slave><DC><CycleTime0>500000</CycleTime0></DC></Slave></Config></EtherCATConfig>");
            await channel.ExecuteAsync("StartBus", Request(("EniPath", file), ("ExpectedCycleUs", 500), ("RequireDc", true)), context, default);
        }
        finally { File.Delete(file); }
        await channel.ExecuteAsync("ConfigureExchange", Request(("ExchangeMode", ExchangeMode.Queue), ("QueuePrefill", 1), ("InitialOutput", new byte[1])), context, default);
        await channel.ExecuteAsync("EnablePi", Request(), context, default);
        await channel.ExecuteAsync("StartAcquisition", Request(("NativeWaitMs", 1), ("HistoryCapacity", 16), ("LatestOnly", false)), context, default);
    }

    private static Task Eventually(Func<bool> predicate) => Eventually(() => Task.FromResult(predicate()));
    private static async Task Eventually(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(3000);
        while (!await predicate()) await Task.Delay(10, timeout.Token);
    }

    private sealed class ChannelProbe : ZlgNativeApi
    {
        public readonly ConcurrentQueue<byte[]> Frames = new();
        private byte[] _frame = [0];
        public Action? OnBusStatus;
        public int CloseCalls;
        private bool _running, _pi;
        public override IntPtr Open(string boardInterface, uint channel) => new(1);
        public override void SetTimeout(IntPtr handle, uint timeoutMs) { }
        public override BoardDetails GetBoardInfo(IntPtr handle) => new("HPCIe-2E", "test", "1", 1, 1, 0);
        public override void Close(IntPtr handle) => Interlocked.Increment(ref CloseCalls);
        public override void StartBus(IntPtr handle, string eniPath) => _running = true;
        public override void StopBus(IntPtr handle) => _running = false;
        public override bool IsBusRunning(IntPtr handle) => _running;
        public override bool IsPiEnabled(IntPtr handle) => _pi;
        public override void EnablePi(IntPtr handle, bool enabled) => _pi = enabled;
        public override uint GetBusStatus(IntPtr handle) { OnBusStatus?.Invoke(); return 0; }
        public override byte GetMasterState(IntPtr handle) => 8;
        public override EniDetails GetEniInfo(IntPtr handle) => new(500, 500, 1, 1);
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) => 1;
        public override PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave) => new(0, 1);
        public override void SetPiAccessMode(IntPtr handle, PiAccessMode mode) { }
        public override void WritePi(IntPtr handle, PiArea area, uint offset, byte[] data) { }
        public override void PushOutput(IntPtr handle, bool clearOldQueue, uint timeoutMs) { }
        public override uint GetInputQueueSize(IntPtr handle) => (uint)Frames.Count;
        public override uint GetOutputQueueSize(IntPtr handle) => 0;
        public override bool PopInput(IntPtr handle, bool latest, uint timeoutMs)
        {
            if (!Frames.TryDequeue(out var frame)) return false;
            _frame = frame;
            return true;
        }
        public override byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length) => _frame.ToArray();
        public override MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves) => new(0xA01, 8, 1, 0, []);
    }
}

public class ChannelContextProxy : DispatchProxy
{
    private object? _registry;
    private int _writes;
    public int WriteCount => Volatile.Read(ref _writes);
    public ChannelRegistryProxy Registry => (ChannelRegistryProxy)(_registry ??= Create<IResourceRegistry, ChannelRegistryProxy>());
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "get_Resources": _ = Registry; return _registry;
            case "HasVariable": return args?[0]?.ToString() == "Locals.Sample";
            case "SetVariable": Interlocked.Increment(ref _writes); return null;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class ChannelRegistryProxy : DispatchProxy
{
    public ConcurrentDictionary<string, object> Items { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        string key = args.FirstOrDefault() as string ?? "";
        switch (method!.Name)
        {
            case "TryGet":
                bool found = Items.TryGetValue(key, out object? value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = found ? value : null;
                return found;
            case "TryAdd": return Items.TryAdd(key, args[1]!);
            case "Contains": return Items.ContainsKey(key);
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
