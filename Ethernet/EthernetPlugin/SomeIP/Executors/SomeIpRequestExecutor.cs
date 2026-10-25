using System.Net;
using System.Net.Sockets;
using Ethernet.SomeIP.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpRequestExecutor : IStepExecutor
{
    private static int _sessionCounter;
    private readonly IEthernetNetworkIo _network;
    public SomeIpRequestExecutor() : this(EthernetNetworkIo.Instance) { }
    internal SomeIpRequestExecutor(IEthernetNetworkIo network) => _network = network;

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
            var serializer = new SomeIpRequestPlugin().CreateSerializer();
            var setting = (SomeIpRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.Transport)) throw new ArgumentOutOfRangeException(nameof(setting.Transport), "SOME/IP Transport 无效，禁止回退到 UDP");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "SOME/IP 请求总超时必须大于 0");
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
                MessageType      = SomeIpMessageType.Request,
                Payload          = SomeIpHelper.ParsePayload(payloadStr),
            };

            context.Log(
                $"SOME/IP 请求({setting.Transport}): {host}:{port} Service=0x{message.ServiceId:X4} Method=0x{message.MethodId:X4} [{SomeIpHelper.ToHex(message.Payload)}]");

            using var operation = budget = new EthernetOperationBudget(setting.TimeoutMs, "SOME/IP 请求", cancellationToken, CheckExecution);
            SomeIpMessage response;
            if (setting.Transport == SomeIpTransport.Tcp)
                response = await RequestOverTcpAsync(host, port, message, operation);
            else
                response = await RequestOverUdpAsync(host, port, message, operation);

            var responseHex = SomeIpHelper.ToHex(response.Payload);
            context.Log(
                $"SOME/IP 响应: ReturnCode=0x{response.ReturnCode:X2} [{responseHex}]");
            cancellationToken.ThrowIfCancellationRequested(); // 日志不占通信预算，但取消后不能继续发布结果。
            CheckExecution();

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            if (response.MessageType == SomeIpMessageType.Error || response.ReturnCode != 0x00)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Value = responseHex,
                        Error = new ErrorInfo { Message = $"SOME/IP 错误响应: ReturnCode=0x{response.ReturnCode:X2}" }
                    }
                };

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = responseHex }
            };
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
                    Error = ErrorInfo.FromException(ex, $"SOME/IP 请求失败: {ex.Message}")
                }
            };
        }
    }

    private static bool IsMatch(SomeIpMessage? msg, SomeIpMessage request)
        => msg != null && msg.ServiceId == request.ServiceId && msg.MethodId == request.MethodId
           && msg.ClientId == request.ClientId && msg.SessionId == request.SessionId
           && msg.MessageType is SomeIpMessageType.Response or SomeIpMessageType.Error;

    private async Task<SomeIpMessage> RequestOverUdpAsync(
        string host, int port, SomeIpMessage message, EthernetOperationBudget budget)
    {
        using var udp = new UdpClient();
        var endpoint = await EthernetNetworkIo.ResolveEndpointAsync(_network, host, port, udp.Client.AddressFamily, budget);
        budget.Check();
        udp.Connect(endpoint); // IP endpoint overload never performs synchronous DNS.
        var encoded = message.Encode();
        budget.BeforeSubmission();
        await _network.SendUdpAsync(udp, endpoint, encoded, budget.Token);
        budget.Phase = "等待完整响应";
        budget.Check();

        while (true)
        {
            budget.Check();
            var recv = await udp.ReceiveAsync(budget.Token);
            var msg = SomeIpMessage.TryDecode(recv.Buffer);
            if (IsMatch(msg, message)) { budget.Check(); return msg!; }
        }
    }

    private async Task<SomeIpMessage> RequestOverTcpAsync(
        string host, int port, SomeIpMessage message, EthernetOperationBudget budget)
    {
        using var tcp = new TcpClient();
        budget.Phase = "TCP 连接及地址解析";
        budget.Check();
        await _network.ConnectTcpAsync(tcp, host, port, budget.Token);
        var stream = tcp.GetStream();
        var encoded = message.Encode();
        budget.BeforeSubmission();
        await _network.WriteTcpAsync(stream, encoded, budget.Token);
        budget.Phase = "等待完整响应";
        budget.Check();

        while (true)
        {
            budget.Check();
            var msg = await ReceiveTcpMessageAsync(stream, budget.Token);
            if (IsMatch(msg, message)) { budget.Check(); return msg; }
        }
    }

    /// <summary>从 TCP 流按 SOME/IP 报文头 Length 字段拆包读取一条完整报文。</summary>
    internal static async Task<SomeIpMessage> ReceiveTcpMessageAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[SomeIpMessage.HeaderLength];
        await ReadExactAsync(stream, header, ct);
        var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
        if (length < 8 || length > 16 * 1024 * 1024)
            throw new InvalidDataException($"SOME/IP TCP 报文长度非法: {length}");

        var payloadLen = (int)length - 8;
        var full = new byte[SomeIpMessage.HeaderLength + payloadLen];
        header.CopyTo(full, 0);
        if (payloadLen > 0)
            await ReadExactAsync(stream, full.AsMemory(SomeIpMessage.HeaderLength), ct);

        return SomeIpMessage.TryDecode(full)
               ?? throw new InvalidDataException("SOME/IP TCP 报文解析失败");
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer[read..], ct);
            if (n == 0) throw new IOException("TCP 连接已被对端关闭");
            read += n;
        }
    }
}
