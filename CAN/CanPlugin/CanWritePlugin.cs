using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN;

public sealed class CanWritePlugin : StepPluginBase<CanWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.CanWrite";
    public override string DisplayName => "CAN_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        向已打开的 CAN 通道发送一帧报文。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | CanId | string([ExpressionField]) | 是 | — | CAN ID，如 0x7DF |
        | FrameType | 枚举 | 否 | Standard | 可选值：Standard, Extended |
        | Data | string([ExpressionField]) | 是 | — | 十六进制数据，如 "02 10 01" |
        | UseFdFrame | bool | 否 | false | 是否使用 CAN FD 帧 |
        | EnableLog | bool | 否 | true | 是否输出发送日志 |

        ## 行为

        - 连接不存在或发送失败时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Read`：接收报文
        """;

    public override IStepExecutor CreateExecutor() => new CanWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName} ID={s.CanId} [{s.Data}]";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (CanWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_020", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("CAN_020E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.CanId))
            errors.Add(StepSettingError.Error("CAN_021", "CAN ID 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.CanId, context.ExecutionContext, out var canIdErr))
            errors.Add(StepSettingError.Error("CAN_021E", $"CanId 表达式无效: {canIdErr}"));
        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Warning("CAN_W20", "发送数据为空"));
        else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var dataValidErr))
            errors.Add(StepSettingError.Error("CAN_W20E", $"Data 表达式无效: {dataValidErr}"));
        else if (s.Data.Length >= 2 && s.Data.StartsWith('"') && s.Data.EndsWith('"'))
        {
            var hex = s.Data[1..^1].Trim().Replace(" ", "");
            if (hex.Length > 0 && (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^[0-9A-Fa-f]+$")))
                errors.Add(StepSettingError.Warning("CAN_W21", "发送数据应为偶数位十六进制字符串（如 02 10 01）"));
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
