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
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.UdpReceiveSetting)new UdpReceivePlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (!s.UseConnection && (s.LocalPort < 0 || s.LocalPort > 65535))
            errors.Add(StepSettingError.Error("ETH_501", "LocalPort 必须在 0~65535 范围内，0 表示系统分配临时端口"));

        if (s.ExpectedLength is < 0 or > 65507)
            errors.Add(StepSettingError.Error("ETH_502", "ExpectedLength 必须在 0~65507 字节内"));
        if (!Enum.IsDefined(s.Encoding) || (!s.UseConnection && !Enum.IsDefined(s.BindMode)))
            errors.Add(StepSettingError.Error("ETH_505", "UDP 编码或绑定模式无效"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_503", "接收超时时间必须大于 0"));

        Validation.EthernetOutputValidator.Check(context, s.ResultVariable, errors);
        if (s.UseConnection)
        {
            if (string.IsNullOrWhiteSpace(s.ConnectionName) || !context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out _))
                errors.Add(StepSettingError.Error("ETH_UDP01", "ConnectionName 必须为有效的字符串表达式"));
            Validation.EthernetLifecycleValidator.CheckPrecedingUdpOpen(context, s.ConnectionName, errors);
        }
        Validation.EthernetOutputValidator.Check(context, s.RemoteEndpointVariable, errors);
        if (!string.IsNullOrWhiteSpace(s.ResultVariable) && s.ResultVariable == s.RemoteEndpointVariable)
            errors.Add(StepSettingError.Error("ETH_UDP03", "结果变量与来源端点变量不能使用同一路径"));
        return errors;
    }
}
