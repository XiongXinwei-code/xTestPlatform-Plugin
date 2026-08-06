using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipDiagRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.DiagRequest";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipDiagRequestEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipDiagRequestSetting)new DoipDiagRequestPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.SessionName))
            errors.Add(StepSettingError.Error("DOIP_301", "SessionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SessionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_302", $"SessionName 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.TargetAddress))
            errors.Add(StepSettingError.Error("DOIP_303", "TargetAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TargetAddress, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("DOIP_304", $"TargetAddress 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.RequestData))
            errors.Add(StepSettingError.Error("DOIP_305", "RequestData 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RequestData, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("DOIP_306", $"RequestData 表达式无效: {e3}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("DOIP_307", "TimeoutMs 必须大于 0"));

        return errors;
    }
}
