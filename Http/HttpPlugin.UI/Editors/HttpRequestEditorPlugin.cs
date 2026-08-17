using System.Windows;
using Http.Models;
using Http.UI.Validation;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpRequest";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpRequestEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpRequestPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpRequestSetting)new HttpRequestPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ClientName))
            errors.Add(StepSettingError.Error("HTTP_020", "客户端标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ClientName, context.ExecutionContext, out var nameErr))
            errors.Add(StepSettingError.Error("HTTP_020E", $"ClientName 表达式无效: {nameErr}"));

        if (string.IsNullOrWhiteSpace(s.Path))
            errors.Add(StepSettingError.Error("HTTP_021", "请求路径不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Path, context.ExecutionContext, out var pathErr))
            errors.Add(StepSettingError.Error("HTTP_021E", $"Path 表达式无效: {pathErr}"));

        if (s.ContentType != BodyContentType.None)
        {
            if (string.IsNullOrWhiteSpace(s.Body))
                errors.Add(StepSettingError.Error("HTTP_022", "已选择请求体类型，请求体内容不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.Body, context.ExecutionContext, out var bodyErr))
                errors.Add(StepSettingError.Error("HTTP_022E", $"Body 表达式无效: {bodyErr}"));
        }

        HttpEditorValidationHelper.CheckVariable(context, s.ResponseVariable, typeof(string), "HTTP_023", errors);
        HttpEditorValidationHelper.CheckVariable(context, s.StatusCodeVariable, typeof(int), "HTTP_024", errors);
        HttpEditorValidationHelper.CheckVariable(context, s.ElapsedVariable, typeof(int), "HTTP_025", errors);
        HttpEditorValidationHelper.CheckHeaders(context, s.Headers, "HTTP_026", errors);

        HttpLifecycleValidator.CheckPrecedingCreate(context.SequenceFile, context.Block, context.CurrentStep, s.ClientName, errors);
        return errors;
    }
}
