using System.Net.Sockets;
using Ethernet.SomeIP.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpFireAndForgetExecutor : IStepExecutor
{
    private static int _sessionCounter;
    private readonly IEthernetNetworkIo _network;
    public SomeIpFireAndForgetExecutor() : this(EthernetNetworkIo.Instance) { }
    internal SomeIpFireAndForgetExecutor(IEthernetNetworkIo network) => _network = network;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        EthernetOperationBudget? budget = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Ethernet");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new SomeIpFireAndForgetPlugin().CreateSerializer();
            var setting = (SomeIpFireAndForgetSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.Transport)) throw new ArgumentOutOfRangeException(nameof(setting.Transport), "SOME/IP Transport 无效，禁止回退到 UDP");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (setting.SendTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.SendTimeoutMs), "发送总超时必须大于 0");
            var host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            CheckExecution();
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);
            CheckExecution();
            var serviceStr = await EthernetExecutorHelper.EvalStringAsync(setting.ServiceId, context);
            CheckExecution();
            var methodStr = await EthernetExecutorHelper.EvalStringAsync(setting.MethodId, context);
            CheckExecution();
            var clientStr = await EthernetExecutorHelper.EvalStringAsync(setting.ClientId, context);
            CheckExecution();
            var ifVerStr = await EthernetExecutorHelper.EvalStringAsync(setting.InterfaceVersion, context);
            CheckExecution();
            var payloadStr = await EthernetExecutorHelper.EvalStringAsync(setting.Payload, context);
            CheckExecution();

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

            using var operation = budget = new EthernetOperationBudget(setting.SendTimeoutMs, "SOME/IP 无响应发送", cancellationToken, CheckExecution);
            if (setting.Transport == SomeIpTransport.Tcp)
            {
                using var tcp = new TcpClient();
                operation.Phase = "TCP 连接及地址解析";
                operation.Check();
                await _network.ConnectTcpAsync(tcp, host, port, operation.Token);
                var stream = tcp.GetStream();
                var encoded = message.Encode();
                operation.BeforeSubmission();
                await _network.WriteTcpAsync(stream, encoded, operation.Token);
                operation.Check();
            }
            else
            {
                using var udp = new UdpClient();
                var endpoint = await EthernetNetworkIo.ResolveEndpointAsync(_network, host, port, udp.Client.AddressFamily, operation);
                var encoded = message.Encode();
                operation.BeforeSubmission();
                await _network.SendUdpAsync(udp, endpoint, encoded, operation.Token);
                operation.Check();
            }

            context.Log(
                $"SOME/IP FireAndForget 已发送({setting.Transport}): {host}:{port} Service=0x{message.ServiceId:X4} Method=0x{message.MethodId:X4} [{SomeIpHelper.ToHex(message.Payload)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted,
                Value = budget?.SubmissionStarted == true ? "已尝试发送，远端执行结果未知；未自动重发" : "本次尚未提交发送" } };
        }
        catch (OperationCanceledException ex) when (budget?.IsExpired == true)
        {
            var timeout = budget.CreateTimeout(ex);
            return ExecutionResults.FromException(timeout, timeout.Message);
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"SOME/IP FireAndForget 发送失败: {ex.Message}")
                }
            };
        }
    }
}
