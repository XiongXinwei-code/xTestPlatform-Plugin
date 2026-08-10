using System.Net;
using UdpCommunication.Helpers;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using UdpCommunication.Transport;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Executors;

public sealed class UdpSendExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new UdpSendPlugin().CreateSerializer();
            var s = (UdpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

            var remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteAddress), s.RemotePort);
            var payload = UdpMessageCodec.Encode(requestData, s.RequestFormat);

            context.LogAction?.Invoke(
                $"UDP 发送：{transport!.LocalEndPoint} → {remoteEndpoint}，" +
                $"格式 {s.RequestFormat}，{payload.Length} 字节，" +
                $"内容 {UdpExecutionLog.Preview(requestData)}");

            await transport.SendAsync(payload, remoteEndpoint, cancellationToken);

            context.LogAction?.Invoke($"UDP 发送完成：已发送 {payload.Length} 字节");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = requestData
                }
            };
        }
        catch (OperationCanceledException)
        {
            context.LogAction?.Invoke("UDP 发送已取消");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            context.LogAction?.Invoke($"UDP 发送失败：{ex.Message}");
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
