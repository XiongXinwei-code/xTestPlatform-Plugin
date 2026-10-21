using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class WriteCstTargetExecutor : AxisActionExecutor<WriteCstTargetSetting>
{
    protected override async Task OperateAsync(WriteCstTargetSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.WriteCyclicAsync(OperationMode.CST, values.Number(nameof(setting.TargetTorque)), ct).ConfigureAwait(false);
    }
}
