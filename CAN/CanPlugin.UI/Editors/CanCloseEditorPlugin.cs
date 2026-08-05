using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanClose";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCloseEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanCloseSetting)new CanClosePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_010", "连接标识名不能为空"));
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
