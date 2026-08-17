using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 发送 SOAP 请求并将响应 XML 写入变量
/// </summary>
public sealed class HttpSoapRequestExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpSoapRequestPlugin().CreateSerializer();
        var setting = (HttpSoapRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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
                return Error("服务端点路径未配置");

            var envelope = await Evaluator.EvalStringAsync(setting.Envelope, context);
            if (string.IsNullOrWhiteSpace(envelope))
                return Error("SOAP Envelope 内容未配置");

            var soapAction = await Evaluator.EvalStringAsync(setting.SoapAction, context);

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(path));

            var mediaType = setting.SoapVersion == SoapVersion.Soap12
                ? "application/soap+xml"
                : "text/xml";

            var content = new StringContent(envelope, Encoding.UTF8, mediaType);

            if (setting.SoapVersion == SoapVersion.Soap12)
            {
                // SOAP 1.2 将 action 作为 Content-Type 的参数携带
                if (!string.IsNullOrWhiteSpace(soapAction))
                    content.Headers.ContentType!.Parameters.Add(
                        new NameValueHeaderValue("action", $"\"{soapAction}\""));
            }
            else if (!string.IsNullOrWhiteSpace(soapAction))
            {
                // SOAP 1.1 使用独立的 SOAPAction 请求头
                request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
            }

            request.Content = content;

            foreach (var header in setting.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Name)) continue;
                var value = await Evaluator.EvalStringAsync(header.Value, context);
                request.Headers.Remove(header.Name);
                request.Headers.TryAddWithoutValidation(header.Name, value);
            }

            if (setting.LogPayload)
                context.LogAction?.Invoke($"SOAP 请求: {path} | Action: {soapAction} | {envelope}");

            var stopwatch = Stopwatch.StartNew();
            using var response = await resource.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var elapsedMs = (int)stopwatch.ElapsedMilliseconds;

            if (!string.IsNullOrWhiteSpace(setting.ResponseVariable))
                context.SetVariable(setting.ResponseVariable, responseXml);
            if (!string.IsNullOrWhiteSpace(setting.StatusCodeVariable))
                context.SetVariable(setting.StatusCodeVariable, statusCode);

            if (setting.LogPayload)
                context.LogAction?.Invoke($"SOAP 响应: {statusCode} ({elapsedMs} ms) | {responseXml}");

            var faultMessage = setting.TreatSoapFaultAsFailure ? TryGetFaultMessage(responseXml) : null;
            if (faultMessage != null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Value = statusCode.ToString(),
                        ElapsedMs = elapsedMs,
                        Error = new ErrorInfo { ErrorCode = statusCode, Message = $"服务端返回 SOAP Fault: {faultMessage}" }
                    }
                };
            }

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
                        : new ErrorInfo { ErrorCode = statusCode, Message = $"SOAP 调用返回非成功状态码 {statusCode}: {responseXml}" }
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TaskCanceledException)
        {
            return Error("SOAP 调用超时");
        }
        catch (Exception ex)
        {
            return Error($"SOAP 调用失败: {ex.Message}");
        }
    }

    /// <summary>解析响应中的 SOAP Fault，未包含 Fault 时返回 null</summary>
    private static string? TryGetFaultMessage(string responseXml)
    {
        if (string.IsNullOrWhiteSpace(responseXml)) return null;

        try
        {
            var document = XDocument.Parse(responseXml);
            var fault = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
            if (fault == null) return null;

            // SOAP 1.1 使用 faultstring，SOAP 1.2 使用 Reason/Text
            var reason = fault.Descendants()
                .FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text");

            return string.IsNullOrWhiteSpace(reason?.Value) ? fault.Value.Trim() : reason.Value.Trim();
        }
        catch
        {
            // 响应不是合法 XML 时不按 Fault 处理，交由状态码判定
            return null;
        }
    }

    private static Uri BuildRequestUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(path.TrimStart('/'), UriKind.Relative);

    private static ExecutionResult Error(string message) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = new ErrorInfo { Message = message }
        }
    };
}
