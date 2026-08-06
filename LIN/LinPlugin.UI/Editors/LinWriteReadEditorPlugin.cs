using System.Windows;
using LIN.Models;
using LIN.UI.Validation;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinWriteReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinWriteRead";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinWriteReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinWriteReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinWriteReadSetting)new LinWriteReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_WR01", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.FrameId))
            errors.Add(StepSettingError.Error("LIN_WR02", "帧 ID 不能为空"));
        if (s.ResponseTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("LIN_WR03", "响应超时时间必须大于 0"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return errors;
    }
}
