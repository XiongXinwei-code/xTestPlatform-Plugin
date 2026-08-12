using System.Windows;
using VISA.Helpers;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaOpen";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaOpenSetting)new VisaOpenPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_001", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_001E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.ResourceString))
            errors.Add(StepSettingError.Error("VISA_002", "VISA 资源字符串不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ResourceString, context.ExecutionContext, out var resErr))
            errors.Add(StepSettingError.Error("VISA_002E", $"ResourceString 表达式无效: {resErr}"));
        if (s.OpenTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("VISA_003", "打开超时必须大于 0"));
        if (s.IoTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("VISA_004", "IO 超时必须大于 0"));
        var term = VisaHelper.NormalizeTerminator(s.Terminator);
        if (term[^1] > 0xFF)
            errors.Add(StepSettingError.Error("VISA_005", "终止符必须是单字节字符（如 \\n、\\r\\n），不支持中文等多字节字符"));
        return errors;
    }
}
