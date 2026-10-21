using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class HomeExecutor : AxisActionExecutor<HomeSetting>
{
    protected override async Task OperateAsync(HomeSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        await session.Controller.HomeAsync(values.Number(nameof(setting.Method)), values.Number(nameof(setting.SearchVelocity)), values.Number(nameof(setting.ZeroVelocity)), values.Number(nameof(setting.Acceleration)), ct).ConfigureAwait(false);
    }
}
