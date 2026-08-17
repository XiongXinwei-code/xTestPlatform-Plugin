using System.Windows;
using Http.Models;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpClientCreateEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpClientCreate";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpClientCreateEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpClientCreatePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpClientCreateSetting)new HttpClientCreatePlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ClientName))
            errors.Add(StepSettingError.Error("HTTP_001", "客户端标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ClientName, context.ExecutionContext, out var nameErr))
            errors.Add(StepSettingError.Error("HTTP_001E", $"ClientName 表达式无效: {nameErr}"));

        if (string.IsNullOrWhiteSpace(s.BaseUrl))
            errors.Add(StepSettingError.Error("HTTP_002", "服务基地址不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.BaseUrl, context.ExecutionContext, out var urlErr))
            errors.Add(StepSettingError.Error("HTTP_002E", $"BaseUrl 表达式无效: {urlErr}"));

        if (s.TimeoutMs < 0)
            errors.Add(StepSettingError.Error("HTTP_003", "超时毫秒数不能为负数"));

        switch (s.AuthMode)
        {
            case AuthMode.Basic:
                if (string.IsNullOrWhiteSpace(s.UserName))
                    errors.Add(StepSettingError.Error("HTTP_004", "Basic 认证的用户名不能为空"));
                else if (!context.Evaluator.ValidateExpression(s.UserName, context.ExecutionContext, out var userErr))
                    errors.Add(StepSettingError.Error("HTTP_004E", $"UserName 表达式无效: {userErr}"));
                if (!string.IsNullOrWhiteSpace(s.Password) &&
                    !context.Evaluator.ValidateExpression(s.Password, context.ExecutionContext, out var pwdErr))
                    errors.Add(StepSettingError.Error("HTTP_005E", $"Password 表达式无效: {pwdErr}"));
                break;

            case AuthMode.BearerToken:
                if (string.IsNullOrWhiteSpace(s.Token))
                    errors.Add(StepSettingError.Error("HTTP_006", "Bearer Token 不能为空"));
                else if (!context.Evaluator.ValidateExpression(s.Token, context.ExecutionContext, out var tokenErr))
                    errors.Add(StepSettingError.Error("HTTP_006E", $"Token 表达式无效: {tokenErr}"));
                break;

            case AuthMode.ClientCertificate:
                if (string.IsNullOrWhiteSpace(s.ClientCertPath))
                    errors.Add(StepSettingError.Error("HTTP_007", "客户端证书路径不能为空"));
                else if (!context.Evaluator.ValidateExpression(s.ClientCertPath, context.ExecutionContext, out var certErr))
                    errors.Add(StepSettingError.Error("HTTP_007E", $"ClientCertPath 表达式无效: {certErr}"));
                if (!string.IsNullOrWhiteSpace(s.ClientCertPassword) &&
                    !context.Evaluator.ValidateExpression(s.ClientCertPassword, context.ExecutionContext, out var certPwdErr))
                    errors.Add(StepSettingError.Error("HTTP_008E", $"ClientCertPassword 表达式无效: {certPwdErr}"));
                break;
        }

        if (s.IgnoreServerCertificateErrors)
            errors.Add(StepSettingError.Warning("HTTP_009", "已忽略服务端证书校验，仅建议在内网自签证书环境使用"));

        foreach (var header in s.DefaultHeaders)
        {
            if (string.IsNullOrWhiteSpace(header.Name))
            {
                errors.Add(StepSettingError.Error("HTTP_010", "默认请求头中存在名称为空的行"));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(header.Value) &&
                !context.Evaluator.ValidateExpression(header.Value, context.ExecutionContext, out var headerErr))
                errors.Add(StepSettingError.Error("HTTP_010E", $"请求头 {header.Name} 的值表达式无效: {headerErr}"));
        }

        return errors;
    }
}
