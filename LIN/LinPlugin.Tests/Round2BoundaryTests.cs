using System.Reflection;
using LIN.Adapters;
using LIN.Adapters.NiXnet;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace LIN.Tests;

public sealed class Round2BoundaryTests
{
    public static IEnumerable<object[]> Plugins() => typeof(LinOpenPlugin).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t)).Select(t => new object[] { t.FullName! });

    [Theory, MemberData(nameof(Plugins))]
    public async Task PrecanceledCallerDoesNotParseOrLookup(string type)
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = (IStepPlugin)Activator.CreateInstance(typeof(LinOpenPlugin).Assembly.GetType(type)!)!;
        state.PayloadOverride = [0xc1];
        using var ct = new CancellationTokenSource(); ct.Cancel();
        Assert.Equal(TestStatus.Aborted, (await state.Plugin.CreateExecutor().ExecuteAsync(context, ct.Token)).StepResult.Status);
        Assert.Empty(state.Items);
    }

    [Theory]
    [InlineData("Close")]
    [InlineData("Write")]
    [InlineData("Read")]
    [InlineData("WriteRead")]
    [InlineData("Sleep")]
    [InlineData("Wakeup")]
    public async Task FinalLogCancellationCannotReturnCompleted(string operation)
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = (IStepPlugin)Activator.CreateInstance(typeof(LinOpenPlugin).Assembly.GetType($"LIN.Lin{operation}Plugin")!)!;
        state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        state.Setting.GetType().GetProperty("ConnectionName")!.SetValue(state.Setting, "\"LIN1\"");
        state.Setting.GetType().GetProperty("PostWakeupDelayMs")?.SetValue(state.Setting, 0);
        state.Setting.GetType().GetProperty("PostSleepDelayMs")?.SetValue(state.Setting, 0);
        var raw = new ImmediateAdapter(); var owner = new LinConnectionResource(raw);
        state.Items[LinHelper.GetAdapterKey("LIN1")] = owner;
        using var ct = new CancellationTokenSource();
        state.OnLog = message => { if (operation != "WriteRead" || message.Contains("LIN 响应")) ct.Cancel(); };
        try { Assert.Equal(TestStatus.Aborted, (await state.Plugin.CreateExecutor().ExecuteAsync(context, ct.Token)).StepResult.Status); }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeStageMayNotContinueAfterCallerCanceled(bool opening)
    {
        var driver = new StageDriver { BlockOpen = opening };
        var owner = new LinConnectionResource(new NiXnetLinAdapter(driver));
        using var ct = new CancellationTokenSource();
        if (!opening) await owner.OpenAsync(new() { Channel = "LIN1" }, 5000, default);
        Task operation = opening ? owner.OpenAsync(new() { Channel = "LIN1" }, 5000, ct.Token)
            : Task.Run(() => owner.Wakeup(true, 5000, ct.Token));
        try
        {
            await driver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ct.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            driver.Release.Set();
            await owner.CloseAsync(5000);
            Assert.Equal(opening ? 1 : 2, driver.Creates);
            Assert.Equal(0, driver.RemoteWakes);
        }
        finally { driver.Release.Set(); await owner.CloseAsync(5000); }
    }

    sealed class StageDriver : NiLinIo
    {
        public bool BlockOpen;
        public int Creates, RemoteWakes;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release = new();
        public override int Create(string a, string b, string c, string d, uint m, out uint s)
        { s = (uint)Interlocked.Increment(ref Creates); if (BlockOpen && s == 1) Block(); return 0; }
        public override int Set(uint s, uint p, uint z, ref uint v)
        {
            if (p == NiXnetLinApi.nxPropSession_IntfLINSleep)
            {
                if (v == NiXnetLinApi.nxLINSleep_LocalSleep) Block();
                if (v == NiXnetLinApi.nxLINSleep_RemoteWake) Interlocked.Increment(ref RemoteWakes);
            }
            return 0;
        }
        void Block() { Entered.TrySetResult(); if (!Release.Wait(10000)) throw new TimeoutException("test barrier"); }
        public override int Start(uint s, uint p) => 0;
        public override int Stop(uint s, uint p) => 0;
        public override int Clear(uint s) => 0;
        public override void Check(int s, string c = "") { if (s != 0) throw new Exception(c); }
    }

    sealed class ImmediateAdapter : ILinAdapter
    {
        public bool IsConnected => true;
        public void Open(LinAdapterConfig c) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(LinFrame f) { }
        public LinFrame? Read(int t, CancellationToken c = default) => new() { FrameId = 0x12, Data = [1] };
        public LinFrame? Read(byte id, int t, CancellationToken c = default) => Read(t, c);
        public void Wakeup(bool r = true) { }
        public void Sleep(bool r = true) { }
    }
}
