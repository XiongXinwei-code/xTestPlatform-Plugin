using System.Reflection;
using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanOpenExpressionDeadlineTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Slow_or_cancelled_inputs_never_open_a_driver_even_when_the_old_evaluation_completes(int blockedInput, bool cancel)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context; state.Plugin = new CanOpenPlugin();
        state.Setting = new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = "channel", ConnectionName = "name", ZlgRemoteAddress = "address", TimeoutMs = 80 };
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int factories = 0, evaluations = 0;
        string[] values = ["CANFDNET_TCP/0/0", "CAN1", "192.168.1.1"];
        var executor = new CanOpenExecutor(_ => { factories++; return new FakeAdapter(); }, (_, _) =>
        {
            int index = evaluations++;
            if (index == blockedInput) { entered.TrySetResult(); return pending.Task; }
            return Task.FromResult(values[index]);
        });
        using var caller = new CancellationTokenSource();
        var operation = executor.ExecuteAsync(context, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); if (cancel) caller.Cancel();
        var result = await operation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
        Assert.Equal(0, factories); Assert.Null(state.Adapter);
        pending.SetResult(values[blockedInput]);
        await Task.Yield();
        Assert.Equal(0, factories); Assert.Equal(blockedInput + 1, evaluations); Assert.Null(state.Adapter);
    }
}
