using System.Windows;
using LIN.Models;
using LIN.UI.Validation;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinSleepEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinSleep";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinSleepEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinSleepPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinSleepSetting)new LinSleepPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_SL01", "连接标识名不能为空"));
        else
        {
            if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var err))
                errors.Add(StepSettingError.Error("LIN_SL02", $"ConnectionName 表达式无效: {err}"));

            if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
                LinLifecycleValidator.CheckPrecedingOpen(
                    context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        }

        if (s.PostSleepDelayMs < 0)
            errors.Add(StepSettingError.Error("LIN_SL03", "入睡后延时不能为负数"));

        return errors;
    }
}
