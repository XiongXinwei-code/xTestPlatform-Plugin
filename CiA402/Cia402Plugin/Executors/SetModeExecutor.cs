using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class SetModeExecutor : AxisActionExecutor<SetModeSetting>
{
    protected override async Task OperateAsync(SetModeSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.SetModeAsync(setting.Mode, ct).ConfigureAwait(false);
    }
}
