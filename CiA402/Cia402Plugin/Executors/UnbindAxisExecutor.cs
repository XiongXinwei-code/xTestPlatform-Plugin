using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class UnbindAxisExecutor : ExecutorBase<UnbindAxisSetting>
{
    protected override async Task RunAsync(UnbindAxisSetting setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken runToken)
    {
        var key = AxisSession.Key(values.Text(nameof(setting.AxisName)));
        if (!context.Resources.TryGet<AxisSession>(key, out var session)) return;
        await session.CloseAsync().WaitAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        context.Resources.Remove(key);
    }
}
