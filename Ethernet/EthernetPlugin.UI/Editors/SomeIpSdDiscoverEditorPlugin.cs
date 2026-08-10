using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpSdDiscoverEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.SdDiscover";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpSdDiscoverEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpSdDiscoverSetting)new SomeIpSdDiscoverPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.MulticastAddress))
            errors.Add(StepSettingError.Error("SOMEIP_401", "MulticastAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.MulticastAddress, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("SOMEIP_402", $"MulticastAddress 表达式无效: {e1}"));

        if (s.Port <= 0 || s.Port > 65535)
            errors.Add(StepSettingError.Error("SOMEIP_403", "Port 必须在 1~65535 之间"));

        if (string.IsNullOrWhiteSpace(s.ServiceId))
            errors.Add(StepSettingError.Error("SOMEIP_404", "ServiceId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ServiceId, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("SOMEIP_405", $"ServiceId 表达式无效: {e2}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SOMEIP_406", "TimeoutMs 必须大于 0"));

        return errors;
    }
}
