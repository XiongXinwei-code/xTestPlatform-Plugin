using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.UI;

public sealed class VisaWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaWriteEditorView();
        view.ViewModel.AttachSerializer(new VisaWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaWriteSetting)new VisaWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.Command))
            errors.Add(StepSettingError.Error("VISA_021", "SCPI 命令不能为空"));
        return errors;
    }
}
