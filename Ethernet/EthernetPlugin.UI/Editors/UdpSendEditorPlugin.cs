using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class UdpSendEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.UdpSend";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/udp_send.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpSendEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.UdpSendSetting)new UdpSendPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.RemoteHost))
            errors.Add(StepSettingError.Error("ETH_401", "RemoteHost 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemoteHost, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("ETH_402", $"RemoteHost 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.RemotePort))
            errors.Add(StepSettingError.Error("ETH_403", "RemotePort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemotePort, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("ETH_404", $"RemotePort 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Error("ETH_405", "Data 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("ETH_406", $"Data 表达式无效: {e3}"));

        if (!s.UseConnection && (s.LocalPort < 0 || s.LocalPort > 65535))
            errors.Add(StepSettingError.Error("ETH_407", "LocalPort 必须在 0~65535 范围内"));
        if (s.SendTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_408", "发送总超时 SendTimeoutMs 必须大于 0"));

        if (s.UseConnection)
        {
            if (string.IsNullOrWhiteSpace(s.ConnectionName) || !context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out _))
                errors.Add(StepSettingError.Error("ETH_UDP01", "ConnectionName 必须为有效的字符串表达式"));
            Validation.EthernetLifecycleValidator.CheckPrecedingUdpOpen(context, s.ConnectionName, errors);
        }
        if (!Enum.IsDefined(s.Encoding)) errors.Add(StepSettingError.Error("ETH_UDP04", "UDP 编码无效"));
        return errors;
    }
}
