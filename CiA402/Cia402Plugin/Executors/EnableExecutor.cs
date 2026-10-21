using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class EnableExecutor : AxisActionExecutor<EnableSetting>
{
    protected override async Task OperateAsync(EnableSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.EnableAsync(ct).ConfigureAwait(false);
    }
}
