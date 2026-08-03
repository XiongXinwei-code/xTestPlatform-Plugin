using System.Diagnostics;
using UdpCommunication.StepPlugin.Display;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin.Executors;

public sealed class UdpSendAndReceiveExecutor : IStepExecutor
{
    private readonly IStepSettingSerializer _serializer;
    private readonly IUdpTransport _transport;

    public UdpSendAndReceiveExecutor(IStepSettingSerializer serializer, IUdpTransport? transport = null)
    {
        _serializer = serializer;
        _transport = transport ?? new UdpTransport();
    }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("未找到当前步骤配置");
            var setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (UdpSendAndReceiveSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
                : (UdpSendAndReceiveSetting)_serializer.CreateDefault();
            var endpoint = new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort);
            var validation = UdpSettingsValidator.ValidateEndpoint(endpoint);
            if (validation is not null || setting.ReceiveTimeoutMs <= 0)
            {
                return ConfigurationError(context, validation ?? "接收超时必须大于 0");
            }

            var responseVariableError = UdpResponseVariable.Validate(setting.ResponseVariable, context);
            if (responseVariableError is not null)
            {
                return ConfigurationError(context, responseVariableError);
            }

            var payload = UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat);
            Log(
                context,
                $"UDP 发送开始：{endpoint.LocalAddress}:{endpoint.LocalPort} → " +
                $"{endpoint.RemoteAddress}:{endpoint.RemotePort}，格式 {setting.RequestFormat}，" +
                $"{payload.Length} 字节，内容 {UdpDescriptionFormatter.Preview(setting.RequestData)}");
            Log(context, $"UDP 等待回复：超时 {setting.ReceiveTimeoutMs} ms");
            var reply = await _transport.SendAndReceiveAsync(
                endpoint,
                payload,
                TimeSpan.FromMilliseconds(setting.ReceiveTimeoutMs),
                cancellationToken);
            var actual = UdpMessageCodec.Decode(reply.Payload, setting.ReplyFormat);
            Log(
                context,
                $"UDP 收到回复：来源 {reply.RemoteEndPoint}，格式 {setting.ReplyFormat}，" +
                $"{reply.Payload.Length} 字节，内容 {UdpDescriptionFormatter.Preview(actual)}");

            var responseVariable = UdpResponseVariable.NormalizePath(setting.ResponseVariable);
            if (responseVariable is not null)
            {
                context.SetVariable(responseVariable, actual);
                Log(context, $"UDP 写入回复变量 {responseVariable}：{UdpDescriptionFormatter.Preview(actual)}");
            }

            var matched = string.IsNullOrEmpty(setting.ExpectedReply)
                || UdpMessageCodec.IsMatch(reply.Payload, UdpMessageCodec.Encode(setting.ExpectedReply, setting.ReplyFormat), setting.MatchMode);
            if (string.IsNullOrEmpty(setting.ExpectedReply))
            {
                Log(context, "UDP 未配置期望回复：收到任意回复即通过");
            }
            else
            {
                Log(
                    context,
                    $"UDP 回复匹配{(matched ? "通过" : "失败")}：模式 {setting.MatchMode}，" +
                    $"期望 {UdpDescriptionFormatter.Preview(setting.ExpectedReply)}，" +
                    $"实际 {UdpDescriptionFormatter.Preview(actual)}");
            }

            return new ExecutionResult { StepResult = new StepResult { Status = matched ? TestStatus.Passed : TestStatus.Failed, Value = actual, UpperBound = setting.ExpectedReply, Condition = setting.MatchMode == UdpReplyMatchMode.Exact ? "完全相等" : "包含指定字段" } };
        }
        catch (TimeoutException ex)
        {
            Log(context, $"UDP 接收超时：{ex.Message}");
            return Fail(ex.Message);
        }
        catch (OperationCanceledException)
        {
            Log(context, "UDP 收发已取消");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            Log(context, $"UDP 收发失败：{ex.Message}");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } };
        }
    }

    private static ExecutionResult Fail(string message) => new() { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = message } } };

    private static ExecutionResult ConfigurationError(IExecutionContext context, string message)
    {
        Log(context, $"UDP 配置错误：{message}");
        return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = message } } };
    }

    private static void Log(IExecutionContext context, string message)
    {
        try
        {
            context.LogAction?.Invoke(message);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"UDP 平台日志输出失败：{ex.Message}");
        }
    }
}
