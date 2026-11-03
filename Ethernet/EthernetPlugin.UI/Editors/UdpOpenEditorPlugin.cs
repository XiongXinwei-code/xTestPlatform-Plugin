using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class UdpOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.UdpOpen";
    public string IconPath => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_open.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpOpenEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        var s = (Models.UdpOpenSetting)new UdpOpenPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName) || !context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out _))
            errors.Add(StepSettingError.Error("ETH_UDP01", "ConnectionName 必须为有效的字符串表达式"));
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("ETH_UDP02", "TimeoutMs 必须大于 0"));
        if (string.IsNullOrWhiteSpace(s.LocalAddress) || !context.Evaluator.ValidateExpression(s.LocalAddress, context.ExecutionContext, out _))
            errors.Add(StepSettingError.Error("ETH_UO02", "LocalAddress 必须为有效的本机 IP 字符串表达式"));
        if (s.LocalPort is < 0 or > 65535) errors.Add(StepSettingError.Error("ETH_UO03", "LocalPort 必须为 0~65535"));
        Validation.EthernetOutputValidator.Check(context, s.LocalEndpointVariable, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
