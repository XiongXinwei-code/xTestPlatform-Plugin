using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using VISA.Validation;

namespace VISA;

/// <summary>
/// VISA 写入插件，向仪器发送 SCPI 命令（不读取响应）
/// </summary>
public sealed class VisaWritePlugin : StepPluginBase<VisaWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.VisaWrite";
    public override string DisplayName => "VISA_Write";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        向 VISA 仪器发送 SCPI 命令（只写不读），不等待响应。适用于设置类命令如 *RST、:CONF:VOLT:DC 等。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 VISA 连接标识名 |
        | Command | string([ExpressionField]) | 是 | — | SCPI 命令，如 *RST |

        ## 行为

        - 连接不存在或写入超时时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Query`：查询类命令（写+读）
        - `VISA_BatchWrite`：批量发送多条命令
        """;

    public override IStepExecutor CreateExecutor() => new VisaWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName}: {s.Command}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (VisaWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_020", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_020E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.Command))
            errors.Add(StepSettingError.Error("VISA_021", "SCPI 命令不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Command, context.ExecutionContext, out var cmdErr))
            errors.Add(StepSettingError.Error("VISA_021E", $"Command 表达式无效: {cmdErr}"));
        VisaLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
