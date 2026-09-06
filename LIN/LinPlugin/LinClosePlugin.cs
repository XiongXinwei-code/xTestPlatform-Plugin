using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using LIN.Validation;

namespace LIN;

public sealed class LinClosePlugin : StepPluginBase<LinCloseSetting>, IStepPlugin
{
    public override string StepTypeId   => "IO.LinClose";
    public override string DisplayName  => "LIN_Close";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        关闭 LIN 通道，释放硬件资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 要关闭的连接标识名 |

        ## 行为

        - 关闭后该连接名不可再被其他 LIN 步骤使用

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        """;

    public override IStepExecutor CreateExecutor() => new LinCloseExecutor();

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
        var s = (LinCloseSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_C01", "连接标识名不能为空"));
        else
        {
            if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var err))
                errors.Add(StepSettingError.Error("LIN_C02", $"ConnectionName 表达式无效: {err}"));

            if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
                LinLifecycleValidator.CheckPrecedingOpen(
                    context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        }

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
