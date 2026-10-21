using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class WriteCspTargetExecutor : AxisActionExecutor<WriteCspTargetSetting>
{
    protected override async Task OperateAsync(WriteCspTargetSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.WriteCyclicAsync(OperationMode.CSP, values.Number(nameof(setting.TargetPosition)), ct).ConfigureAwait(false);
    }
}
