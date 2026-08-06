using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipVehicleDiscoveryEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.VehicleDiscovery";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipVehicleDiscoveryEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipVehicleDiscoverySetting)new DoipVehicleDiscoveryPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.BroadcastAddress))
            errors.Add(StepSettingError.Error("DOIP_401", "BroadcastAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.BroadcastAddress, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_402", $"BroadcastAddress 表达式无效: {e1}"));

        if (s.Port <= 0 || s.Port > 65535)
            errors.Add(StepSettingError.Error("DOIP_403", "Port 必须在 1~65535 之间"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("DOIP_404", "TimeoutMs 必须大于 0"));

        return errors;
    }
}
