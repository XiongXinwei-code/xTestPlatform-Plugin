using System.Net.Sockets;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpFireAndForgetExecutor : IStepExecutor
{
    private static int _sessionCounter;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SomeIpFireAndForgetPlugin().CreateSerializer();
        var setting = (SomeIpFireAndForgetSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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
                MessageType      = SomeIpMessageType.RequestNoReturn,
                Payload          = SomeIpHelper.ParsePayload(payloadStr),
            };

            if (setting.Transport == SomeIpTransport.Tcp)
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port, cancellationToken);
                await tcp.GetStream().WriteAsync(message.Encode(), cancellationToken);
            }
            else
            {
                using var udp = new UdpClient();
                udp.Connect(host, port);
                await udp.SendAsync(message.Encode(), cancellationToken);
            }

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"SOME/IP FireAndForget 已发送({setting.Transport}): {host}:{port} Service=0x{message.ServiceId:X4} Method=0x{message.MethodId:X4} [{SomeIpHelper.ToHex(message.Payload)}]");

            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed } };
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
                    Error = new ErrorInfo { Message = $"SOME/IP FireAndForget 发送失败: {ex.Message}" }
                }
            };
        }
    }
}
