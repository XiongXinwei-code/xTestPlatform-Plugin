using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using VISA.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaBatchWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaBatchWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaBatchWriteSetting)new VisaBatchWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_060", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_060E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Error("VISA_061", "至少需要一条 SCPI 命令"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            if (s.Items[i].DelayMs < 0)
                errors.Add(StepSettingError.Error("VISA_063", $"第 {i + 1} 条命令：延时不能为负数"));
        }
        for (int i = 0; i < s.Items.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(s.Items[i].Command))
                errors.Add(StepSettingError.Error("VISA_062", $"第 {i + 1} 行：命令不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.Items[i].Command, context.ExecutionContext, out var cmdErr))
                errors.Add(StepSettingError.Error("VISA_062E", $"第 {i + 1} 行：Command 表达式无效: {cmdErr}"));
        }
        VisaLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
