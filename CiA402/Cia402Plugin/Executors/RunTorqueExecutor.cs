using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class RunTorqueExecutor : AxisActionExecutor<RunTorqueSetting>
{
    protected override async Task OperateAsync(RunTorqueSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.RunTorqueAsync(values.Number(nameof(setting.TargetTorque)), values.Number(nameof(setting.TorqueSlope)), ct).ConfigureAwait(false);
    }
}
