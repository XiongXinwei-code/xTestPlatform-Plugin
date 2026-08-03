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
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderConfigEditorView();
        view.ViewModel.AttachSerializer(new NiDaqEncoderConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderConfigSetting)new NiDaqEncoderConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.CounterChannel)) errors.Add(StepSettingError.Error("E002", "Counter 通道不能为空"));
        return errors;
    }
}
