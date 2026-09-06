using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using NiDaq.Validation;

namespace NiDaq;

public sealed class NiDaqTaskStopPlugin : StepPluginBase<NiDaqTaskStopSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.TaskStop";
    public override string DisplayName => "NiDaq_Task_Stop";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        停止并释放已启动的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的任务名称 |

        ## 行为

        - 停止后任务对象被释放，需重新配置才能再次使用

        ## 相关插件

        - `NiDaq_Task_Start`：启动任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Stop: {s.TaskName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqTaskStopSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_070", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_070E", $"TaskName 表达式无效: {taskNameErr}"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
