using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipDisconnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.Disconnect";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipDisconnectEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipDisconnectSetting)new DoipDisconnectPlugin().CreateSerializer()
                    .Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.SessionName))
            errors.Add(StepSettingError.Error("DOIP_201", "SessionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SessionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_202", $"SessionName 表达式无效: {e1}"));

        return errors;
    }
}
