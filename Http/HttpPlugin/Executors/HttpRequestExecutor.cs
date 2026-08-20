using System.Diagnostics;
using System.Text;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 发送 HTTP REST 请求并将响应写入变量
/// </summary>
public sealed class HttpRequestExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpRequestPlugin().CreateSerializer();
        var setting = (HttpRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            if (!context.Resources.TryGet<HttpClientResource>(HttpHelper.GetClientKey(clientName), out var resource))
                return Error($"未找到 HTTP 客户端: {clientName}，请先执行 Http_ClientCreate 步骤");

            var path = await Evaluator.EvalStringAsync(setting.Path, context);
            if (string.IsNullOrWhiteSpace(path))
                return Error("请求路径未配置");

            using var request = new HttpRequestMessage(
                HttpHelper.ToHttpMethod(setting.Method),
                BuildRequestUri(path));

            foreach (var header in setting.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Name)) continue;
                var value = await Evaluator.EvalStringAsync(header.Value, context);
                request.Headers.Remove(header.Name);
                request.Headers.TryAddWithoutValidation(header.Name, value);
            }

            var mediaType = HttpHelper.ToMediaType(setting.ContentType);
            string body = string.Empty;
            if (mediaType != null)
            {
                body = await Evaluator.EvalStringAsync(setting.Body, context);
                request.Content = new StringContent(body, Encoding.UTF8, mediaType);
            }

            if (setting.LogPayload)
                context.LogAction?.Invoke($"HTTP 请求: {request.Method} {path}{(string.IsNullOrEmpty(body) ? string.Empty : " | " + body)}");

            var stopwatch = Stopwatch.StartNew();
            using var response = await resource.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var elapsedMs = (int)stopwatch.ElapsedMilliseconds;

            if (!string.IsNullOrWhiteSpace(setting.ResponseVariable))
                context.SetVariable(setting.ResponseVariable, content);
            if (!string.IsNullOrWhiteSpace(setting.StatusCodeVariable))
                context.SetVariable(setting.StatusCodeVariable, statusCode);
            if (!string.IsNullOrWhiteSpace(setting.ElapsedVariable))
                context.SetVariable(setting.ElapsedVariable, elapsedMs);

            if (setting.LogPayload)
                context.LogAction?.Invoke($"HTTP 响应: {statusCode} ({elapsedMs} ms) | {content}");

            var succeeded = response.IsSuccessStatusCode || !setting.TreatNonSuccessAsFailure;

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = succeeded ? TestStatus.Passed : TestStatus.Failed,
                    Value = statusCode.ToString(),
                    Condition = "2xx",
                    ElapsedMs = elapsedMs,
                    Error = succeeded
                        ? null
                        : new ErrorInfo { ErrorCode = statusCode, Message = $"HTTP 请求返回非成功状态码 {statusCode}: {content}" }
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TaskCanceledException)
        {
            return Error("HTTP 请求超时");
        }
        catch (Exception ex)
        {
            return Error($"HTTP 请求失败: {ex.Message}", ex);
        }
    }

    /// <summary>路径为绝对 URL 时直接使用，否则作为相对于基地址的相对路径</summary>
    private static Uri BuildRequestUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(path.TrimStart('/'), UriKind.Relative);

    private static ExecutionResult Error(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
