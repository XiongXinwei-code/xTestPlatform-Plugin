using System.Windows;
using LIN.Models;
using LIN.UI.Validation;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinRead";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_read.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinReadSetting)new LinReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        foreach (var path in new[] { s.ResultVariable, s.IdVariable })
        {
            ct.ThrowIfCancellationRequested();
            try { LIN.Helpers.LinHelper.ValidateStringOutput(context.ExecutionContext, path); }
            catch (ArgumentException ex) { errors.Add(StepSettingError.Error("LIN_OUTPUT", ex.Message)); }
        }
        if (!string.IsNullOrWhiteSpace(s.ConnectionName) && !context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var ConnectionNameError))
            errors.Add(StepSettingError.Error("LIN_EXPR_ConnectionName", $"ConnectionName 表达式无效：{ConnectionNameError}"));
        if (!string.IsNullOrWhiteSpace(s.FilterFrameId) && !context.Evaluator.ValidateExpression(s.FilterFrameId, context.ExecutionContext, out var FilterFrameIdError))
            errors.Add(StepSettingError.Error("LIN_EXPR_FilterFrameId", $"FilterFrameId 表达式无效：{FilterFrameIdError}"));

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_R01", "连接标识名不能为空"));
        if (s.ReadTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("LIN_R02", "读取超时时间必须大于 0"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return errors;
    }
}
