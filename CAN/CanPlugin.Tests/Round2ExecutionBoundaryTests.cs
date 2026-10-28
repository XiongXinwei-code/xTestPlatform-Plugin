using System.Reflection;
using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;
using CAN.UDS;
using CAN.UDS.Models;
using CAN.XCP;
using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

public sealed class Round2ExecutionBoundaryTests
{
    [Theory]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Close")]
    [InlineData("UDS")]
    [InlineData("XCP")]
    public async Task FinalHostLogCancellationCannotBecomeCompleted(string operation)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = operation switch { "Read" => new CanReadPlugin(), "Write" => new CanWritePlugin(), "Close" => new CanClosePlugin(), "UDS" => new UdsDiagSessionPlugin(), _ => new XcpDisconnectPlugin() };
        state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        var fake = new FakeAdapter(); var owner = new CanConnectionResource(fake); state.Adapter = owner;
        uint responseId = operation switch { "UDS" => 0x70C, "XCP" => 0x7E8, _ => 0x123 };
        byte[] payload = operation switch { "UDS" => [6, 0x50, 3, 0, 50, 0, 200], "XCP" => [0xFF], _ => [1] };
        if (state.Setting is UdsCommonSetting uds) { uds.TxId = "\"0x704\""; uds.RxId = "\"0x70C\""; }
        if (state.Setting is XcpCommonSetting xcp) { xcp.TxId = "\"0x7E0\""; xcp.RxId = "\"0x7E8\""; }
        fake.Incoming.Add(new() { Id = responseId, Data = payload });
        using var caller = new CancellationTokenSource(); state.OnLog = _ => caller.Cancel();
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(result.StepResult.Status == TestStatus.Aborted, result.StepResult.Error?.Message ?? result.StepResult.Status.ToString());
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task RawReadDoesNotPublishAfterRegistryOwnerReplacement()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = new CanReadPlugin(); state.Setting = new CanReadSetting { ResultVariable = "Locals.result" };
        var replacement = new CanConnectionResource(new FakeAdapter());
        var raw = new ReplacingReader(() => state.Adapter = replacement); var original = new CanConnectionResource(raw); state.Adapter = original;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(state.Variables);
            Assert.Same(replacement, state.Adapter); Assert.False(replacement.IsClosed);
        }
        finally { await original.CloseAsync(); await replacement.CloseAsync(); }
    }

    sealed class ReplacingReader(Action replace) : ICanAdapter
    {
        public bool IsConnected => true;
        public void Open(CanAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(CanMessage frame) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) { replace(); return new() { Id = 0x123, Data = [1] }; }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
    }
}
