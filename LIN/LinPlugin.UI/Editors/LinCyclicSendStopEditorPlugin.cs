using System.Windows;
using LIN.Models;
using LIN.UI.Validation;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinCyclicSendStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinCyclicSendStop";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinCyclicSendStopEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinCyclicSendStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinCyclicSendStopSetting)new LinCyclicSendStopPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("LIN_SS01", "任务标识名不能为空"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingCyclicStart(
                context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);

        return errors;
    }
}
