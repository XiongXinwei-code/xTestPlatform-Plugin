using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.UI;

public sealed class VisaReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaRead";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaReadEditorView();
        view.ViewModel.AttachSerializer(new VisaReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaReadSetting)new VisaReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_040", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("VISA_041", "结果变量名不能为空"));
        return errors;
    }
}
