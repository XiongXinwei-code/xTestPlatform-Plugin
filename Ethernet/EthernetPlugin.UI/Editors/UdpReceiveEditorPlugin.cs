using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class UdpReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.UdpReceive";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/udp_receive.png";

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

        if (s.ExpectedLength is < 0 or > 65507)
            errors.Add(StepSettingError.Error("ETH_502", "ExpectedLength 必须在 0~65507 字节内"));
        if (!Enum.IsDefined(s.Encoding) || !Enum.IsDefined(s.BindMode))
            errors.Add(StepSettingError.Error("ETH_505", "UDP 编码或绑定模式无效"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_503", "接收超时时间必须大于 0"));

        Validation.EthernetOutputValidator.Check(context, s.ResultVariable, errors);
        return errors;
    }
}
