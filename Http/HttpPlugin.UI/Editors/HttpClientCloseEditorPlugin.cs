using System.Windows;
using Http.Models;
using Http.UI.Validation;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpClientCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpClientClose";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpClientCloseEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpClientClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpClientCloseSetting)new HttpClientClosePlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ClientName))
            errors.Add(StepSettingError.Error("HTTP_100", "客户端标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ClientName, context.ExecutionContext, out var nameErr))
            errors.Add(StepSettingError.Error("HTTP_100E", $"ClientName 表达式无效: {nameErr}"));

        HttpLifecycleValidator.CheckPrecedingCreate(context.SequenceFile, context.Block, context.CurrentStep, s.ClientName, errors);
        return errors;
    }
}
