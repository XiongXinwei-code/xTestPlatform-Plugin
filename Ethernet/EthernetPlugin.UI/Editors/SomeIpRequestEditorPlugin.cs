using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.Request";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpRequestEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpRequestSetting)new SomeIpRequestPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        ValidateExpr(errors, context, s.RemoteHost, "RemoteHost", "SOMEIP_101", "SOMEIP_102");
        ValidateExpr(errors, context, s.RemotePort, "RemotePort", "SOMEIP_103", "SOMEIP_104");
        ValidateExpr(errors, context, s.ServiceId, "ServiceId", "SOMEIP_105", "SOMEIP_106");
        ValidateExpr(errors, context, s.MethodId, "MethodId", "SOMEIP_107", "SOMEIP_108");
        ValidateExpr(errors, context, s.ClientId, "ClientId", "SOMEIP_109", "SOMEIP_110");
        ValidateExpr(errors, context, s.InterfaceVersion, "InterfaceVersion", "SOMEIP_111", "SOMEIP_112");

        if (!string.IsNullOrWhiteSpace(s.Payload)
            && !context.Evaluator.ValidateExpression(s.Payload, context.ExecutionContext, out var pe))
            errors.Add(StepSettingError.Error("SOMEIP_113", $"Payload 表达式无效: {pe}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SOMEIP_114", "TimeoutMs 必须大于 0"));

        return errors;
    }

    private static void ValidateExpr(List<StepSettingError> errors, StepEditorValidationContext context,
        string value, string fieldName, string emptyCode, string invalidCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(StepSettingError.Error(emptyCode, $"{fieldName} 不能为空"));
        else if (!context.Evaluator.ValidateExpression(value, context.ExecutionContext, out var e))
            errors.Add(StepSettingError.Error(invalidCode, $"{fieldName} 表达式无效: {e}"));
    }
}
