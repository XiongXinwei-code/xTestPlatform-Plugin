using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class TcpReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.TcpReceive";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new TcpReceiveEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
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

        return errors;
    }
}
