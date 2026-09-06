using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.UDS;

public sealed class UdsRoutineControlPlugin : StepPluginBase<UdsRoutineControlSetting>, IStepPlugin
{
    public override string StepTypeId => "UDS.RoutineControl";
    public override string DisplayName => "UDS_RoutineControl";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        执行 ECU 例程控制（UDS 服务 0x31）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ControlType | 枚举 | 否 | Start | 可选值：Start, Stop, RequestResults |
        | RoutineId | string([ExpressionField]) | 是 | — | 例程 ID，如 0xFF00 |
        | OptionRecord | string([ExpressionField]) | 否 | 空 | 输入参数（十六进制），可为空 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_DiagSession`：切换诊断会话
        - `UDS_SecurityAccess`：解锁受保护例程
        """;

    public override IStepExecutor CreateExecutor() => new UdsRoutineControlExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RoutineControl {s.ControlType} RID={s.RoutineId} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsRoutineControlSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
			errors.Add(StepSettingError.Error("UDS_001E", $"ConnectionName 表达式无效: {connErr}"));
		if (string.IsNullOrWhiteSpace(s.TxId))
			errors.Add(StepSettingError.Error("UDS_002", "TX ID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.TxId, context.ExecutionContext, out var txErr))
			errors.Add(StepSettingError.Error("UDS_002E", $"TxId 表达式无效: {txErr}"));
		if (string.IsNullOrWhiteSpace(s.RxId))
			errors.Add(StepSettingError.Error("UDS_003", "RX ID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.RxId, context.ExecutionContext, out var rxErr))
			errors.Add(StepSettingError.Error("UDS_003E", $"RxId 表达式无效: {rxErr}"));
		if (s.ResponseTimeoutMs == 0 || s.ResponseTimeoutMs < -1)
			errors.Add(StepSettingError.Error("UDS_005", "响应超时必须大于 0，或为 -1 表示永不超时"));
		if (string.IsNullOrWhiteSpace(s.RoutineId))
			errors.Add(StepSettingError.Error("UDS_C001", "Routine ID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.RoutineId, context.ExecutionContext, out var ridErr))
			errors.Add(StepSettingError.Error("UDS_C001E", $"RoutineId 表达式无效: {ridErr}"));

		if (!string.IsNullOrWhiteSpace(s.ResultVariable))
		{
			if (!context.ExecutionContext.HasVariable(s.ResultVariable))
				errors.Add(StepSettingError.Error("UDS_C002", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
			else
			{
				var val = context.ExecutionContext.GetVariable(s.ResultVariable);
				if (val is not null && val is not string)
					errors.Add(StepSettingError.Warning("UDS_C003", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
			}
		}

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
