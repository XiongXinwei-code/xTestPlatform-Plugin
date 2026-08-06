using System.Windows;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class TcpSendEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Ethernet.TcpSend";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new TcpSendEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.TcpSendSetting)new TcpSendPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("ETH_201", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("ETH_202", $"ConnectionName 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Error("ETH_203", "Data 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("ETH_204", $"Data 表达式无效: {e2}"));

        return errors;
    }
}
