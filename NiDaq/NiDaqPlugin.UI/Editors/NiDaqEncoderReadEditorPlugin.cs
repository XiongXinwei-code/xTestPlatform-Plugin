using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using NiDaq.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqEncoderReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.EncoderRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqEncoderReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderSetting)new NiDaqEncoderReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_080", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable)) errors.Add(StepSettingError.Error("DAQ_081", "结果变量不能为空"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.Block, context.CurrentStep, s.TaskName, errors);
        return errors;
    }
}
