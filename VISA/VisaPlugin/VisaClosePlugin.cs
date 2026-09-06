using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using VISA.Validation;

namespace VISA;

/// <summary>
/// VISA 关闭会话插件，关闭并释放仪器连接资源
/// </summary>
public sealed class VisaClosePlugin : StepPluginBase<VisaCloseSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.VisaClose";
    public override string DisplayName => "VISA_Close";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        关闭指定的 VISA 仪器会话并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的连接标识名 |

        ## 行为

        - 连接不存在时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        """;

    public override IStepExecutor CreateExecutor() => new VisaCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (VisaCloseSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_010", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_010E", $"ConnectionName 表达式无效: {connErr}"));
        VisaLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
