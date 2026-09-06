using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using NiDaq.Validation;

namespace NiDaq;

public sealed class NiDaqSyncReadPlugin : StepPluginBase<NiDaqSyncReadSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.SyncRead";
    public override string DisplayName => "NiDaq_Sync_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        从已启动的同步采集任务中读取 AI 和编码器对齐数据，可导出为文件并/或将结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的同步任务名 |
        | SamplesToRead | int | 否 | -1 | 读取样本数，-1=读取所有可用 |
        | ReadTimeoutMs | int | 否 | 10000 | 读取超时 ms |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，必须为波形类型（Waveform），写入 WaveformData（AI 通道 + Encoder 通道） |
        | ExportFormat | 枚举 | 否 | Csv | 可选值：Csv, Tdms, Variable, CsvAndVariable, TdmsAndVariable |
        | SaveToFile | bool | 否 | false | 是否将采集数据保存到文件 |
        | OutputDirectory | string([ExpressionField]) | 否 | 空 | 输出文件目录 |
        | MaxFileSizeMB | int | 否 | 500 | 单文件大小上限 MB |

        ## 行为

        - 需先通过 NiDaq_Task_Start 启动任务

        ## 相关插件

        - `NiDaq_Sync_Config`：配置同步任务
        - `NiDaq_Task_Start` / `NiDaq_Task_Stop`：启停任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqSyncReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Read: {s.TaskName} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("DAQ_030", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_030E", $"TaskName 表达式无效: {taskNameErr}"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_031", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_032", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
            NiDaqVariableValidator.CheckWaveformVariable(context.ExecutionContext, s.ResultVariable, "DAQ_033", errors);
        if (s.SaveToFile && string.IsNullOrWhiteSpace(s.OutputDirectory))
            errors.Add(StepSettingError.Warning("DAQ_W31", "启用存盘时建议指定输出目录"));
        else if (s.SaveToFile && !string.IsNullOrWhiteSpace(s.OutputDirectory)
            && !context.Evaluator.ValidateExpression(s.OutputDirectory, context.ExecutionContext, out var dirErr))
            errors.Add(StepSettingError.Error("DAQ_036", $"OutputDirectory 表达式无效: {dirErr}"));
        if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
            errors.Add(StepSettingError.Error("DAQ_034", "读取超时必须大于 0，或为 -1 表示永不超时"));
        if (s.SaveToFile && s.MaxFileSizeMB <= 0)
            errors.Add(StepSettingError.Error("DAQ_035", "最大文件大小必须大于 0"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
