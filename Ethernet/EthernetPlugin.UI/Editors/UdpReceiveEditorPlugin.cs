using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class UdpReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.UdpReceive";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpReceiveEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.UdpReceiveSetting)new UdpReceivePlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (s.LocalPort < 1 || s.LocalPort > 65535)
            errors.Add(StepSettingError.Error("ETH_501", "LocalPort 必须在 1~65535 范围内"));

        if (s.ExpectedLength < 0)
            errors.Add(StepSettingError.Error("ETH_502", "ExpectedLength 不能小于 0"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_503", "接收超时时间必须大于 0"));

        return errors;
    }
}
