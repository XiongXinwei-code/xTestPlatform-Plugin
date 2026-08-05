using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqDoWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DoWrite";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDoWriteEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDoWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDoWriteSetting)new NiDaqDoWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("DAQ_100", "物理通道不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Channel, context.ExecutionContext, out var chErr))
            errors.Add(StepSettingError.Error("DAQ_100E", $"Channel 表达式无效: {chErr}"));
        if (string.IsNullOrWhiteSpace(s.Value)) errors.Add(StepSettingError.Error("DAQ_101", "输出值不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Value, context.ExecutionContext, out var valErr))
            errors.Add(StepSettingError.Error("DAQ_101E", $"Value 表达式无效: {valErr}"));
        return errors;
    }
}
