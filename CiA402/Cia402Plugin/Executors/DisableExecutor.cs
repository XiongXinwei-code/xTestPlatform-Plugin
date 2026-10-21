using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class DisableExecutor : AxisActionExecutor<DisableSetting>
{
    protected override async Task OperateAsync(DisableSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.DisableAsync(ct).ConfigureAwait(false);
    }
}
