using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using LIN.Validation;

namespace LIN;

public sealed class LinCyclicSendStopPlugin : StepPluginBase<LinCyclicSendStopSetting>, IStepPlugin
{
    public override string StepTypeId   => "IO.LinCyclicSendStop";
    public override string DisplayName  => "LIN_Cyclic_SendStop";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        停止指定名称的 LIN 周期发送任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "LinCyclicTask1" | 要停止的任务标识名 |

        ## 行为

        - 任务不存在时步骤报错

        ## 相关插件

        - `LIN_Cyclic_SendStart`：启动周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop TaskName={s.TaskName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (LinCyclicSendStopSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("LIN_SS01", "任务标识名不能为空"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingCyclicStart(
                context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
