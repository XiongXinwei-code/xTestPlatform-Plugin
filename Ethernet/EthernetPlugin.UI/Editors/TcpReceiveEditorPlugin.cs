using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class TcpReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.TcpReceive";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_receive.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new TcpReceiveEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.TcpReceiveSetting)new TcpReceivePlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("ETH_301", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("ETH_302", $"ConnectionName 表达式无效: {e1}"));

        if (s.ExpectedLength < 0)
            errors.Add(StepSettingError.Error("ETH_303", "ExpectedLength 不能小于 0"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_304", "接收超时时间必须大于 0"));

        if (!Enum.IsDefined(s.Encoding)) errors.Add(StepSettingError.Error("ETH_305", "TCP 接收编码无效"));
        if (s.MaxBytes is < 1 or > 16777216) errors.Add(StepSettingError.Error("ETH_306", "MaxBytes 必须在 1~16777216 内"));
        if (!s.UseDelimiter && s.ExpectedLength > s.MaxBytes) errors.Add(StepSettingError.Error("ETH_307", "ExpectedLength 不能超过 MaxBytes"));
        if (s.UseDelimiter && (string.IsNullOrWhiteSpace(s.DelimiterHex) || !context.Evaluator.ValidateExpression(s.DelimiterHex, context.ExecutionContext, out _)))
            errors.Add(StepSettingError.Error("ETH_308", "DelimiterHex 必须为有效的十六进制字符串表达式，运行时为 1~64 字节且不超过 MaxBytes"));

        Validation.EthernetOutputValidator.Check(context, s.ResultVariable, errors);
        Validation.EthernetLifecycleValidator.CheckPrecedingOpen(context, s.ConnectionName, false, errors);
        return errors;
    }
}
