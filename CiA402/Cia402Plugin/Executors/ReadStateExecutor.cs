using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class ReadStateExecutor : AxisActionExecutor<ReadStateSetting>
{
    protected override bool IsControl => false;
    protected override async Task OperateAsync(ReadStateSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        var status = await session.Controller.ReadStatusAsync(ct).ConfigureAwait(false);
		Output(context, values, session, ct, setting.StatusWordVariable, (double)status);
		Output(context, values, session, ct, setting.StateVariable, AxisController.DecodeState(status).ToString());
		context.Log($"驱动状态 {AxisController.DecodeState(status)}，状态字 0x{status:X4}");
    }
}
