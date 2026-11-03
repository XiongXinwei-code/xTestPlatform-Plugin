using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class UdpCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.UdpClose";
    public string IconPath => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_close.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpCloseEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        var s = (Models.UdpCloseSetting)new UdpClosePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName) || !context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out _))
            errors.Add(StepSettingError.Error("ETH_UDP01", "ConnectionName 必须为有效的字符串表达式"));
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("ETH_UDP02", "TimeoutMs 必须大于 0"));
        Validation.EthernetLifecycleValidator.CheckPrecedingUdpOpen(context, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
