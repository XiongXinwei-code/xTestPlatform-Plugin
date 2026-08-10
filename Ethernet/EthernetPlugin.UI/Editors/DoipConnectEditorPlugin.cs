using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipConnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.Connect";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipConnectEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipConnectSetting)new DoipConnectPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.SessionName))
            errors.Add(StepSettingError.Error("DOIP_101", "SessionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SessionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_102", $"SessionName 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.RemoteHost))
            errors.Add(StepSettingError.Error("DOIP_103", "RemoteHost 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemoteHost, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("DOIP_104", $"RemoteHost 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.RemotePort))
            errors.Add(StepSettingError.Error("DOIP_105", "RemotePort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemotePort, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("DOIP_106", $"RemotePort 表达式无效: {e3}"));

        if (string.IsNullOrWhiteSpace(s.SourceAddress))
            errors.Add(StepSettingError.Error("DOIP_107", "SourceAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SourceAddress, context.ExecutionContext, out var e4))
            errors.Add(StepSettingError.Error("DOIP_108", $"SourceAddress 表达式无效: {e4}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("DOIP_109", "TimeoutMs 必须大于 0"));

        return errors;
    }
}
