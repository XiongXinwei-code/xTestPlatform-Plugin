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
        var operation = "SOAP 请求";
        HttpRequestMessage? pendingRequest = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Http");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new HttpSoapRequestPlugin().CreateSerializer();
            var setting = (HttpSoapRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.SoapVersion)) throw new InvalidOperationException("SOAP 版本无效");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            if (!context.Resources.TryGet<HttpClientResource>(HttpHelper.GetClientKey(clientName), out var resource))
                return Error($"未找到 HTTP 客户端: {clientName}，请先执行 Http_ClientCreate 步骤");

            var path = await Evaluator.EvalStringAsync(setting.Path, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(path))
                return Error("服务端点路径未配置");

            var envelope = await Evaluator.EvalStringAsync(setting.Envelope, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(envelope))
                return Error("SOAP Envelope 内容未配置");

            var soapAction = await Evaluator.EvalStringAsync(setting.SoapAction, context);
            CheckExecution();

            var request = pendingRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(path));
            operation = HttpHelper.DescribeRequest(clientName, resource.Client, request);

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
                CheckExecution();
                request.Headers.Remove(header.Name);
                request.Headers.TryAddWithoutValidation(header.Name, value);
            }

            if (setting.LogPayload)
                context.Log($"SOAP 请求: {path} | Action: {soapAction} | {envelope}");

            CheckExecution();
            resource.CheckPublication(context.Resources, HttpHelper.GetClientKey(clientName), cancellationToken);
            HttpHelper.ValidateRequestDestination(resource, request);
            var sending = resource.SendAsync(request, cancellationToken);
            pendingRequest = null;
            var response = await sending;
            var responseXml = response.Content;
            var statusCode = response.StatusCode;
            var elapsedMs = (int)Math.Min(int.MaxValue, response.ElapsedMs);

            if (setting.LogPayload)
                context.Log($"SOAP 响应: {statusCode} ({elapsedMs} ms) | {responseXml}");
            resource.CheckPublication(context.Resources, HttpHelper.GetClientKey(clientName), cancellationToken);

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResponseVariable))
                context.SetVariable(setting.ResponseVariable, responseXml);
            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.StatusCodeVariable))
                context.SetVariable(setting.StatusCodeVariable, statusCode);

            var faultMessage = setting.TreatSoapFaultAsFailure ? TryGetFaultMessage(responseXml) : null;
            if (faultMessage != null)
            {
                var faultResult = ExecutionResults.Error($"服务端返回 SOAP Fault；{operation}",
                    detail: "服务端包含 Fault；具体内容请查看已配置的响应结果变量",
                    errorCode: statusCode, value: statusCode.ToString());
                faultResult.StepResult.ElapsedMs = elapsedMs;
                return faultResult;
            }

            var succeeded = response.IsSuccess || !setting.TreatNonSuccessAsFailure;

            var result = succeeded
                ? ExecutionResults.Completed(statusCode.ToString())
                : ExecutionResults.Error($"SOAP 调用返回非成功状态码 {statusCode}；{operation}",
                    detail: $"响应长度={responseXml.Length} 字符；响应内容请查看已配置的结果变量",
                    errorCode: statusCode, value: statusCode.ToString());
            result.StepResult.Condition = "2xx";
            result.StepResult.ElapsedMs = elapsedMs;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TaskCanceledException ex)
        {
            return Error($"SOAP 调用超时；{operation}", ex);
        }
        catch (Exception ex)
        {
            return Error($"SOAP 调用失败；{operation}", ex);
        }
        finally { pendingRequest?.Dispose(); }
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

    private static ExecutionResult Error(string message, Exception? ex = null)
        => ex is null ? ExecutionResults.Error(message) : ExecutionResults.FromException(ex, message);
}
