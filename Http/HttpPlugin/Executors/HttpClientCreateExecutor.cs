using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 创建命名 HTTP 客户端并注册到运行期资源表
/// </summary>
public sealed class HttpClientCreateExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpClientCreatePlugin().CreateSerializer();
        var setting = (HttpClientCreateSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            var baseUrl = await Evaluator.EvalStringAsync(setting.BaseUrl, context);
            if (string.IsNullOrWhiteSpace(baseUrl))
                return Error("服务基地址未配置");

            if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute, out _))
                return Error($"服务基地址不是合法的绝对 URL: {baseUrl}");

            var key = HttpHelper.GetClientKey(clientName);
            if (context.Resources.Contains(key))
            {
                if (!setting.ReplaceIfExists)
                    return Error($"HTTP 客户端已存在: {clientName}");

                context.Resources.Remove(key);
            }

            var userName = await Evaluator.EvalStringAsync(setting.UserName, context);
            var password = await Evaluator.EvalStringAsync(setting.Password, context);
            var token = await Evaluator.EvalStringAsync(setting.Token, context);
            var certPath = await Evaluator.EvalStringAsync(setting.ClientCertPath, context);
            var certPassword = await Evaluator.EvalStringAsync(setting.ClientCertPassword, context);

            var client = HttpHelper.BuildClient(
                baseUrl,
                setting.TimeoutMs,
                setting.AuthMode,
                userName,
                password,
                token,
                certPath,
                certPassword,
                setting.IgnoreServerCertificateErrors);

            foreach (var header in setting.DefaultHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Name)) continue;
                var value = await Evaluator.EvalStringAsync(header.Value, context);
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Name, value);
            }

            var resource = new HttpClientResource
            {
                Client = client,
                BaseUrl = baseUrl,
                AuthMode = setting.AuthMode
            };

            context.Resources.Set(key, resource);

            context.LogAction?.Invoke($"HTTP 客户端已创建: {clientName} -> {baseUrl}（认证方式: {setting.AuthMode}）");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = baseUrl }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return Error($"创建 HTTP 客户端失败: {ex.Message}", ex);
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
