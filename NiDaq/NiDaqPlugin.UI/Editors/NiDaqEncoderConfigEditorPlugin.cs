using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqEncoderConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.EncoderConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_encoder_config.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderConfigEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqEncoderConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderConfigSetting)new NiDaqEncoderConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_040", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_040E", $"TaskName 表达式无效: {taskNameErr}"));
        if (string.IsNullOrWhiteSpace(s.CounterChannel)) errors.Add(StepSettingError.Error("DAQ_041", "Counter 通道不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.CounterChannel, context.ExecutionContext, out var chErr))
            errors.Add(StepSettingError.Error("DAQ_041E", $"CounterChannel 表达式无效: {chErr}"));
        if (s.PulsesPerRevolution <= 0) errors.Add(StepSettingError.Error("DAQ_042", "每转脉冲数 (PPR) 必须大于 0"));
        if (!double.IsFinite(s.DistancePerPulse) || s.DistancePerPulse <= 0) errors.Add(StepSettingError.Error("DAQ_043", "每脉冲距离必须为有限正数"));
        if (!Enum.IsDefined(s.Unit)) errors.Add(StepSettingError.Error("DAQ_ENCODER_UNIT", "Unit 无效"));
        if (!Enum.IsDefined(s.DecodingType)) errors.Add(StepSettingError.Error("DAQ_ENCODER_DECODING", "DecodingType 无效"));
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("DAQ_099", "总期限必须大于 0（包含旧资源收尾等待）"));
        return errors;
    }
}
