using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.UDS;

public sealed class UdsWriteDataByIdPlugin : StepPluginBase<UdsWriteDataByIdSetting>, IStepPlugin
{
    public override string StepTypeId => "UDS.WriteDataByID";
    public override string DisplayName => "UDS_WriteDataByID";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Did | string([ExpressionField]) | 是 | — | 数据标识符，如 0xF199 |
        | Data | string([ExpressionField]) | 是 | — | 十六进制写入数据，如 "01 02 03" |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错
        - 通常需先切换会话并完成安全访问

        ## 相关插件

        - `UDS_DiagSession`：切换诊断会话
        - `UDS_SecurityAccess`：解锁 ECU
        - `UDS_ReadDataByID`：通过 DID 读取数据
        """;

    public override IStepExecutor CreateExecutor() => new UdsWriteDataByIdExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteDataByID DID={s.Did} Data=[{s.Data}]";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsWriteDataByIdSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

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
		if (string.IsNullOrWhiteSpace(s.Did))
			errors.Add(StepSettingError.Error("UDS_WD01", "DID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.Did, context.ExecutionContext, out var didErr))
			errors.Add(StepSettingError.Error("UDS_WD01E", $"Did 表达式无效: {didErr}"));
		if (string.IsNullOrWhiteSpace(s.Data))
			errors.Add(StepSettingError.Error("UDS_WD02", "写入数据不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var dataErr))
			errors.Add(StepSettingError.Error("UDS_WD02E", $"Data 表达式无效: {dataErr}"));

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
