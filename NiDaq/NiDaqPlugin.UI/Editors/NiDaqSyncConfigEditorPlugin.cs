using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqSyncConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncConfigEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqSyncConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncConfigSetting)new NiDaqSyncConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_020", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_020E", $"TaskName 表达式无效: {taskNameErr}"));
        if (s.AiChannels.Count == 0) errors.Add(StepSettingError.Error("DAQ_021", "AI 通道列表为空"));
        if (s.EncoderChannels.Count == 0) errors.Add(StepSettingError.Error("DAQ_022", "编码器通道列表为空"));
        if (s.SampleRate <= 0) errors.Add(StepSettingError.Error("DAQ_023", "采样率必须大于 0"));
        if (s.SamplesPerChannel <= 0) errors.Add(StepSettingError.Error("DAQ_024", "每通道采样数必须大于 0"));
        for (int i = 0; i < s.AiChannels.Count; i++)
        {
            var ch = s.AiChannels[i];
            if (string.IsNullOrWhiteSpace(ch.PhysicalChannel))
                errors.Add(StepSettingError.Error("DAQ_025", $"AI 第 {i + 1} 行：物理通道不能为空"));
            if (string.IsNullOrWhiteSpace(ch.ColumnName))
                errors.Add(StepSettingError.Error("DAQ_026", $"AI 第 {i + 1} 行：列名不能为空"));
            if (ch.MinValue >= ch.MaxValue)
                errors.Add(StepSettingError.Error("DAQ_027", $"AI 第 {i + 1} 行：量程下限 ({ch.MinValue}) 必须小于上限 ({ch.MaxValue})"));
        }
        return errors;
    }
}
