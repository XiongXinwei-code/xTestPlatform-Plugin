using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpFireAndForgetEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.FireAndForget";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpFireAndForgetEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpFireAndForgetSetting)new SomeIpFireAndForgetPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        ValidateExpr(errors, context, s.RemoteHost, "RemoteHost", "SOMEIP_201", "SOMEIP_202");
        ValidateExpr(errors, context, s.RemotePort, "RemotePort", "SOMEIP_203", "SOMEIP_204");
        ValidateExpr(errors, context, s.ServiceId, "ServiceId", "SOMEIP_205", "SOMEIP_206");
        ValidateExpr(errors, context, s.MethodId, "MethodId", "SOMEIP_207", "SOMEIP_208");
        ValidateExpr(errors, context, s.ClientId, "ClientId", "SOMEIP_209", "SOMEIP_210");
        ValidateExpr(errors, context, s.InterfaceVersion, "InterfaceVersion", "SOMEIP_211", "SOMEIP_212");

        if (!string.IsNullOrWhiteSpace(s.Payload)
            && !context.Evaluator.ValidateExpression(s.Payload, context.ExecutionContext, out var pe))
            errors.Add(StepSettingError.Error("SOMEIP_213", $"Payload 表达式无效: {pe}"));

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
