using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using NiDaq.Validation;

namespace NiDaq;

public sealed class NiDaqTaskStartPlugin : StepPluginBase<NiDaqTaskStartSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.TaskStart";
    public override string DisplayName => "NiDaq_Task_Start";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        启动已配置的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要启动的任务名称 |

        ## 行为

        - 任务不存在时步骤报错

        ## 相关插件

        - `NiDaq_AI_Config` / `NiDaq_Encoder_Config` / `NiDaq_Sync_Config`：配置任务
        - `NiDaq_Task_Stop`：停止任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Start: {s.TaskName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqTaskStartSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_060", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_060E", $"TaskName 表达式无效: {taskNameErr}"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
