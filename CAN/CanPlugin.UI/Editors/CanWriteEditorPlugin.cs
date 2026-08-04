using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanWrite";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanWriteEditorView();
        view.ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanWriteSetting)new CanWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.CanId))
            errors.Add(StepSettingError.Error("CAN_021", "CAN ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Warning("CAN_W20", "发送数据为空"));
        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
