using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class WriteCsvTargetExecutor : AxisActionExecutor<WriteCsvTargetSetting>
{
    protected override async Task OperateAsync(WriteCsvTargetSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.WriteCyclicAsync(OperationMode.CSV, values.Number(nameof(setting.TargetVelocity)), ct).ConfigureAwait(false);
    }
}
