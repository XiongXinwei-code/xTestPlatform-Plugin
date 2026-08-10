using System.Net;
using UdpCommunication.Helpers;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using UdpCommunication.Transport;
using UdpCommunication.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Executors;

public sealed class UdpSendAndReceiveExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new UdpSendAndReceivePlugin().CreateSerializer();
            var s = (UdpSendAndReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (!TryResolveTransport(context, s.OpenStepAddress, out var transport, out var errorMessage))
            {
                context.LogAction?.Invoke($"UDP 错误：{errorMessage}");
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = errorMessage }
                    }
                };
            }

            var remoteAddress = await Evaluator.EvalStringAsync(s.RemoteAddress, context);
            var requestData = await Evaluator.EvalStringAsync(s.RequestData, context);
            var expectedReply = await Evaluator.EvalStringAsync(s.ExpectedReply, context);

            if (s.ReceiveTimeoutMs <= 0)
            {
                context.LogAction?.Invoke("UDP 配置错误：接收超时必须大于 0");
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "接收超时必须大于 0" }
                    }
                };
            }

            var responseVariableError = UdpResponseVariable.Validate(s.ResponseVariable, context);
            if (responseVariableError is not null)
            {
                context.LogAction?.Invoke($"UDP 配置错误：{responseVariableError}");
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = responseVariableError }
                    }
                };
            }

            var remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteAddress), s.RemotePort);
            var payload = UdpMessageCodec.Encode(requestData, s.RequestFormat);

            context.LogAction?.Invoke(
                $"UDP 发送：{transport!.LocalEndPoint} → {remoteEndpoint}，" +
                $"格式 {s.RequestFormat}，{payload.Length} 字节，" +
                $"内容 {UdpExecutionLog.Preview(requestData)}");

            context.LogAction?.Invoke($"UDP 等待回复：超时 {s.ReceiveTimeoutMs} ms");

            var reply = await transport.SendAndReceiveAsync(
                payload,
                remoteEndpoint,
                TimeSpan.FromMilliseconds(s.ReceiveTimeoutMs),
                cancellationToken);

            var actual = UdpMessageCodec.Decode(reply.Payload, s.ReplyFormat);

            context.LogAction?.Invoke(
                $"UDP 收到回复：来源 {reply.RemoteEndPoint}，" +
                $"格式 {s.ReplyFormat}，{reply.Payload.Length} 字节，" +
                $"内容 {UdpExecutionLog.Preview(actual)}");

            var responseVariable = UdpResponseVariable.NormalizePath(s.ResponseVariable);
            if (responseVariable is not null)
            {
                context.SetVariable(responseVariable, actual);
                context.LogAction?.Invoke($"UDP 写入回复变量 {responseVariable}：{UdpExecutionLog.Preview(actual)}");
            }

            var matched = string.IsNullOrEmpty(expectedReply)
                || UdpMessageCodec.IsMatch(
                    reply.Payload,
                    UdpMessageCodec.Encode(expectedReply, s.ReplyFormat),
                    s.MatchMode);

            context.LogAction?.Invoke(
                $"UDP 回复匹配{(matched ? "通过" : "失败")}：" +
                $"模式 {s.MatchMode}，" +
                $"期望 {UdpExecutionLog.Preview(expectedReply)}，" +
                $"实际 {UdpExecutionLog.Preview(actual)}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = matched ? TestStatus.Passed : TestStatus.Failed,
                    Value = actual,
                    UpperBound = expectedReply,
                    Condition = s.MatchMode == UdpReplyMatchMode.Exact ? "完全相等" : "包含指定字段"
                }
            };
        }
        catch (TimeoutException ex)
        {
            context.LogAction?.Invoke($"UDP 接收超时：{ex.Message}");
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Failed,
                    Error = new ErrorInfo { Message = ex.Message }
                }
            };
        }
        catch (OperationCanceledException)
        {
            context.LogAction?.Invoke("UDP 收发已取消");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            context.LogAction?.Invoke($"UDP 收发失败：{ex.Message}");
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = ex.Message }
                }
            };
        }
    }

    private static bool TryResolveTransport(
        IExecutionContext context, string openStepAddress,
        out IUdpTransport? transport, out string errorMessage)
    {
        transport = null;
        if (string.IsNullOrWhiteSpace(openStepAddress))
        {
            errorMessage = "未指定 OpenStepAddress（请先创建一个 UDP_Open 步骤并在此处选择）";
            return false;
        }

        var key = UdpHelper.GetConnectionKey(openStepAddress);
        if (!context.CurrentStep!.RuntimeData.TryGetValue(key, out var obj) || obj is not IUdpTransport t)
        {
            errorMessage = $"连接 {key} 未打开，请先执行引用的 UDP_Open 步骤";
            return false;
        }

        transport = t;
        errorMessage = string.Empty;
        return true;
    }
}
