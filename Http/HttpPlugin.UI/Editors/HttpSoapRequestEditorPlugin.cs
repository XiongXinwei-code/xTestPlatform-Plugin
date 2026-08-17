using System.Windows;
using Http.Models;
using Http.UI.Validation;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpSoapRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpSoapRequest";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpSoapRequestEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpSoapRequestPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpSoapRequestSetting)new HttpSoapRequestPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ClientName))
            errors.Add(StepSettingError.Error("HTTP_040", "客户端标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ClientName, context.ExecutionContext, out var nameErr))
            errors.Add(StepSettingError.Error("HTTP_040E", $"ClientName 表达式无效: {nameErr}"));

        if (string.IsNullOrWhiteSpace(s.Path))
            errors.Add(StepSettingError.Error("HTTP_041", "服务端点路径不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Path, context.ExecutionContext, out var pathErr))
            errors.Add(StepSettingError.Error("HTTP_041E", $"Path 表达式无效: {pathErr}"));

        if (string.IsNullOrWhiteSpace(s.Envelope))
            errors.Add(StepSettingError.Error("HTTP_042", "SOAP Envelope 内容不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Envelope, context.ExecutionContext, out var envErr))
            errors.Add(StepSettingError.Error("HTTP_042E", $"Envelope 表达式无效: {envErr}"));

        if (!string.IsNullOrWhiteSpace(s.SoapAction) &&
            !context.Evaluator.ValidateExpression(s.SoapAction, context.ExecutionContext, out var actErr))
            errors.Add(StepSettingError.Error("HTTP_043E", $"SoapAction 表达式无效: {actErr}"));

        if (s.SoapVersion == SoapVersion.Soap11 && string.IsNullOrWhiteSpace(s.SoapAction))
            errors.Add(StepSettingError.Warning("HTTP_044", "SOAP 1.1 通常要求提供 SOAPAction，留空可能被服务端拒绝"));

        HttpEditorValidationHelper.CheckVariable(context, s.ResponseVariable, typeof(string), "HTTP_045", errors);
        HttpEditorValidationHelper.CheckVariable(context, s.StatusCodeVariable, typeof(int), "HTTP_046", errors);
        HttpEditorValidationHelper.CheckHeaders(context, s.Headers, "HTTP_047", errors);

        HttpLifecycleValidator.CheckPrecedingCreate(context.SequenceFile, context.Block, context.CurrentStep, s.ClientName, errors);
        return errors;
    }
}
