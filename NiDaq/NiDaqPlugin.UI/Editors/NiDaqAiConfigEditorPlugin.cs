using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqAiConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiConfigEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiConfigSetting)new NiDaqAiConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_001", "任务名称不能为空"));
        if (s.Channels.Count == 0) errors.Add(StepSettingError.Error("DAQ_002", "AI 通道列表为空"));
        if (s.SampleRate <= 0) errors.Add(StepSettingError.Error("DAQ_003", "采样率必须大于 0"));
        if (s.SamplesPerChannel <= 0) errors.Add(StepSettingError.Error("DAQ_004", "每通道采样数必须大于 0"));
        return errors;
    }
}
