using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class StopMotionExecutor : AxisActionExecutor<StopMotionSetting>
{
    protected override async Task OperateAsync(StopMotionSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.StopAsync(ct).ConfigureAwait(false);
    }
}
