using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.UDS;

public sealed class UdsSecurityAccessPlugin : StepPluginBase<UdsSecurityAccessSetting>, IStepPlugin
{
    public override string StepTypeId => "UDS.SecurityAccess";
    public override string DisplayName => "UDS_SecurityAccess";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SecurityLevel | int | 否 | 1 | 安全等级，奇数如 1/3/5 |
        | SeedVariable | string(变量路径) | 是 | — | 存储 ECU 返回 Seed 的变量名，KeyExpression 中通过此名引用 |
        | KeyExpression | 表达式(byte[]) | 是 | — | Key 计算表达式，如 new byte[]{(byte)(Seed[0]^0xA5)} |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 bool（解锁是否成功） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - 先请求 Seed，用 KeyExpression 计算 Key 后发送，ECU 确认后解锁成功
        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_DiagSession`：先切换到非默认会话
        - `UDS_WriteDataByID` / `UDS_RoutineControl`：解锁后执行受保护操作
        """;

    public override IStepExecutor CreateExecutor() => new UdsSecurityAccessExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SecurityAccess Level={s.SecurityLevel} (TX={s.TxId}, RX={s.RxId})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsSecurityAccessSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

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
		if (s.SecurityLevel <= 0 || s.SecurityLevel % 2 == 0)
			errors.Add(StepSettingError.Error("UDS_S001", "安全级别必须为正奇数（如 1、3、5）"));
		if (string.IsNullOrWhiteSpace(s.SeedVariable))
			errors.Add(StepSettingError.Error("UDS_S005", "SeedVariable 不能为空，KeyExpression 需通过此变量名引用 Seed"));
		if (string.IsNullOrWhiteSpace(s.KeyExpression))
			errors.Add(StepSettingError.Error("UDS_S004", "Key 计算表达式不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.KeyExpression, context.ExecutionContext, out var keyErr))
			errors.Add(StepSettingError.Error("UDS_S004E", $"KeyExpression 表达式无效: {keyErr}"));

		if (!string.IsNullOrWhiteSpace(s.ResultVariable))
		{
			if (!context.ExecutionContext.HasVariable(s.ResultVariable))
				errors.Add(StepSettingError.Error("UDS_S002", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
			else
			{
				var val = context.ExecutionContext.GetVariable(s.ResultVariable);
				if (val is not null && val is not bool)
					errors.Add(StepSettingError.Warning("UDS_S003", $"变量 {s.ResultVariable} 类型不匹配，期望 bool，实际类型 {val.GetType().Name}"));
			}
		}

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
