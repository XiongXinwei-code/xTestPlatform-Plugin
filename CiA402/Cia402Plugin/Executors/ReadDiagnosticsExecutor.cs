using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class ReadDiagnosticsExecutor : AxisActionExecutor<ReadDiagnosticsSetting>
{
    protected override bool IsControl => false;
    protected override async Task OperateAsync(ReadDiagnosticsSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        double? error = null;
		long? hardware = null;
		if (!string.IsNullOrWhiteSpace(setting.HardwareActiveVariable) && session.Configuration.DiagnosticMask == 0)
			throw new InvalidOperationException("未配置硬件诊断异常掩码，不能输出异常判定");
		if (!string.IsNullOrWhiteSpace(setting.ErrorCodeVariable)) error = await session.Controller.ReadAsync(0x603F, 2, false, ct).ConfigureAwait(false);
		if (!string.IsNullOrWhiteSpace(setting.HardwareValueVariable) || !string.IsNullOrWhiteSpace(setting.HardwareActiveVariable)) hardware = await session.Controller.ReadHardwareAsync(ct).ConfigureAwait(false);
		if (!error.HasValue && !hardware.HasValue) throw new ArgumentException("至少配置一项诊断输出变量");
		if (error.HasValue) Output(context, values, session, ct, setting.ErrorCodeVariable, error.Value);
		if (hardware.HasValue)
		{
			Output(context, values, session, ct, setting.HardwareValueVariable, (double)hardware.Value);
			Output(context, values, session, ct, setting.HardwareActiveVariable, session.Controller.HardwareActive(hardware.Value));
		}
    }
}
