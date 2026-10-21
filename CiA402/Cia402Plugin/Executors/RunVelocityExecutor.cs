using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class RunVelocityExecutor : AxisActionExecutor<RunVelocitySetting>
{
    protected override async Task OperateAsync(RunVelocitySetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.RunVelocityAsync(values.Number(nameof(setting.TargetVelocity)), values.Number(nameof(setting.Acceleration)), values.Number(nameof(setting.Deceleration)), ct).ConfigureAwait(false);
    }
}
