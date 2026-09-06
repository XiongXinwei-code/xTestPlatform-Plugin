using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.UDS;

public sealed class UdsReadDtcPlugin : StepPluginBase<UdsReadDtcSetting>, IStepPlugin
{
    public override string StepTypeId => "UDS.ReadDTC";
    public override string DisplayName => "UDS_ReadDTC";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SubFunction | byte | 否 | 0x02 | 子功能，如 0x02=报告 DTC 及状态 |
        | StatusMask | byte | 否 | 0xFF | DTC 状态掩码 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制 DTC 数据） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_ClearDTC`：清除故障码
        """;

    public override IStepExecutor CreateExecutor() => new UdsReadDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDTC SubFunc=0x{s.SubFunction:X2} Mask=0x{s.StatusMask:X2} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsReadDtcSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

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

		if (!string.IsNullOrWhiteSpace(s.ResultVariable))
		{
			if (!context.ExecutionContext.HasVariable(s.ResultVariable))
				errors.Add(StepSettingError.Error("UDS_T002", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
			else
			{
				var val = context.ExecutionContext.GetVariable(s.ResultVariable);
				if (val is not null && val is not string)
					errors.Add(StepSettingError.Warning("UDS_T003", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
			}
		}

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
