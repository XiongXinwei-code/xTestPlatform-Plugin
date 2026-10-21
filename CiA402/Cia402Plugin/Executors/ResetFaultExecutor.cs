using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class ResetFaultExecutor : AxisActionExecutor<ResetFaultSetting>
{
    protected override async Task OperateAsync(ResetFaultSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.ResetFaultAsync(ct).ConfigureAwait(false);
    }
}
