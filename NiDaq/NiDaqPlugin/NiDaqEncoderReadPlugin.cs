using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using NiDaq.Validation;

namespace NiDaq;

public sealed class NiDaqEncoderReadPlugin : StepPluginBase<NiDaqEncoderSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.EncoderRead";
    public override string DisplayName => "NiDaq_Encoder_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        从已配置的编码器任务中读取当前位置值，存入指定变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的编码器任务名 |
        | ReadTimeoutMs | int | 否 | 5000 | 读取超时 ms |
        | ResultVariable | string(变量路径) | 是 | — | 结果变量名，写入类型为 double（位置值） |

        ## 行为

        - 需先通过 NiDaq_Task_Start 启动任务

        ## 相关插件

        - `NiDaq_Encoder_Config`：配置编码器任务
        - `NiDaq_Task_Start` / `NiDaq_Task_Stop`：启停任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqEncoderReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Read: {s.TaskName} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_080", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_080E", $"TaskName 表达式无效: {taskNameErr}"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_081", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_082", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not double)
                errors.Add(StepSettingError.Error("DAQ_083", $"变量 {s.ResultVariable} 类型不匹配，期望 double，实际类型 {val.GetType().Name}"));
        }
        if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
            errors.Add(StepSettingError.Error("DAQ_084", "读取超时必须大于 0，或为 -1 表示永不超时"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
