using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpSubscribeEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.Subscribe";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpSubscribeEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpSubscribeSetting)new SomeIpSubscribePlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.LocalPort))
            errors.Add(StepSettingError.Error("SOMEIP_301", "LocalPort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.LocalPort, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("SOMEIP_302", $"LocalPort 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.ServiceId))
            errors.Add(StepSettingError.Error("SOMEIP_303", "ServiceId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ServiceId, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("SOMEIP_304", $"ServiceId 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.EventId))
            errors.Add(StepSettingError.Error("SOMEIP_305", "EventId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.EventId, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("SOMEIP_306", $"EventId 表达式无效: {e3}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SOMEIP_307", "TimeoutMs 必须大于 0"));

        return errors;
    }
}
