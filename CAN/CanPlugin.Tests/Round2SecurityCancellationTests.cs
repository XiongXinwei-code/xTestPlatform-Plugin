using System.Reflection;
using CAN.UDS;
using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class Round2SecurityCancellationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KeyCalculationPreservesCallerCancellationClassification(bool cancelCaller)
    {
        using var caller = new CancellationTokenSource();
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Plugin = new UdsSecurityAccessPlugin();
        state.Setting = new UdsSecurityAccessSetting { ConnectionName = "\"CAN1\"", TxId = "\"0x704\"", RxId = "\"0x70C\"", SecurityLevel = 1 };
        var raw = new FakeAdapter(); state.Adapter = raw;
        raw.OnWrite = _ => raw.Incoming.Add(new() { Id = 0x70C, Data = [3, 0x67, 1, 0x42, 0, 0, 0, 0] });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new UdsSecurityAccessExecutor(async (_, _) =>
        {
            entered.TrySetResult(); await finish.Task;
            throw new OperationCanceledException("key evaluation cancelled", caller.Token);
        });
        var pending = executor.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancelCaller) caller.Cancel();
            finish.TrySetResult();
            var result = await pending;
            Assert.Equal(cancelCaller ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Single(raw.Sent); // Only the completed seed request; no key submission.
        }
        finally { finish.TrySetResult(); await pending; }
    }
}
