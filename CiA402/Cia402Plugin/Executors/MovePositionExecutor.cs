using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class MovePositionExecutor : AxisActionExecutor<MovePositionSetting>
{
    protected override async Task OperateAsync(MovePositionSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.MovePositionAsync(values.Number(nameof(setting.TargetPosition)), values.Number(nameof(setting.ProfileVelocity)), values.Number(nameof(setting.Acceleration)), values.Number(nameof(setting.Deceleration)), setting.Relative, setting.WaitForTarget, ct).ConfigureAwait(false);
    }
}
