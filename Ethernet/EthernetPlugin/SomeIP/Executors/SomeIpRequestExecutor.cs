using System.Net;
using System.Net.Sockets;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpRequestExecutor : IStepExecutor
{
    private static int _sessionCounter;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SomeIpRequestPlugin().CreateSerializer();
        var setting = (SomeIpRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);
            var serviceStr = await EthernetExecutorHelper.EvalStringAsync(setting.ServiceId, context);
            var methodStr = await EthernetExecutorHelper.EvalStringAsync(setting.MethodId, context);
            var clientStr = await EthernetExecutorHelper.EvalStringAsync(setting.ClientId, context);
            var ifVerStr = await EthernetExecutorHelper.EvalStringAsync(setting.InterfaceVersion, context);
            var payloadStr = await EthernetExecutorHelper.EvalStringAsync(setting.Payload, context);

            var port = SomeIpHelper.ParsePort(portStr, "RemotePort");
            var message = new SomeIpMessage
            {
                ServiceId        = SomeIpHelper.ParseId(serviceStr, "ServiceId"),
                MethodId         = SomeIpHelper.ParseId(methodStr, "MethodId"),
                ClientId         = SomeIpHelper.ParseId(clientStr, "ClientId"),
                SessionId        = (ushort)(Interlocked.Increment(ref _sessionCounter) & 0xFFFF),
                InterfaceVersion = SomeIpHelper.ParseByte(ifVerStr, "InterfaceVersion"),
                MessageType      = SomeIpMessageType.Request,
                Payload          = SomeIpHelper.ParsePayload(payloadStr),
            };

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"SOME/IP 请求: {host}:{port} Service=0x{message.ServiceId:X4} Method=0x{message.MethodId:X4} [{SomeIpHelper.ToHex(message.Payload)}]");

            using var udp = new UdpClient();
            udp.Connect(host, port);
            await udp.SendAsync(message.Encode(), cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(setting.TimeoutMs);

            SomeIpMessage? response = null;
            while (response == null)
            {
                UdpReceiveResult recv;
                try
                {
                    recv = await udp.ReceiveAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"等待 SOME/IP 响应超时({setting.TimeoutMs}ms)");
                }
                var msg = SomeIpMessage.TryDecode(recv.Buffer);
                if (msg != null && msg.ServiceId == message.ServiceId && msg.MethodId == message.MethodId
                    && msg.SessionId == message.SessionId
                    && msg.MessageType is SomeIpMessageType.Response or SomeIpMessageType.Error)
                    response = msg;
            }

            var responseHex = SomeIpHelper.ToHex(response.Payload);
            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"SOME/IP 响应: ReturnCode=0x{response.ReturnCode:X2} [{responseHex}]");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            if (response.MessageType == SomeIpMessageType.Error || response.ReturnCode != 0x00)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Value = responseHex,
                        Error = new ErrorInfo { Message = $"SOME/IP 错误响应: ReturnCode=0x{response.ReturnCode:X2}" }
                    }
                };

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = responseHex }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"SOME/IP 请求失败: {ex.Message}" }
                }
            };
        }
    }
}
