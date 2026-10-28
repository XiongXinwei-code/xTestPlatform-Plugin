using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class ReadModeExecutor : AxisActionExecutor<ReadModeSetting>
{
    protected override bool IsControl => false;
    protected override async Task OperateAsync(ReadModeSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        var mode = await session.Controller.ReadModeAsync(ct).ConfigureAwait(false);
		Output(context, values, session, ct, setting.ModeVariable, (double)mode);
		context.Log($"实际运行模式：{mode}");
    }
}
