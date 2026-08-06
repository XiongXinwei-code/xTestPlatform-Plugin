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
                    $"SOME/IP 请求({setting.Transport}): {host}:{port} Service=0x{message.ServiceId:X4} Method=0x{message.MethodId:X4} [{SomeIpHelper.ToHex(message.Payload)}]");

            SomeIpMessage response;
            if (setting.Transport == SomeIpTransport.Tcp)
                response = await RequestOverTcpAsync(host, port, message, setting.TimeoutMs, cancellationToken);
            else
                response = await RequestOverUdpAsync(host, port, message, setting.TimeoutMs, cancellationToken);

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

    private static bool IsMatch(SomeIpMessage? msg, SomeIpMessage request)
        => msg != null && msg.ServiceId == request.ServiceId && msg.MethodId == request.MethodId
           && msg.SessionId == request.SessionId
           && msg.MessageType is SomeIpMessageType.Response or SomeIpMessageType.Error;

    private static async Task<SomeIpMessage> RequestOverUdpAsync(
        string host, int port, SomeIpMessage message, int timeoutMs, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.Connect(host, port);
        await udp.SendAsync(message.Encode(), cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        while (true)
        {
            UdpReceiveResult recv;
            try
            {
                recv = await udp.ReceiveAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"等待 SOME/IP 响应超时({timeoutMs}ms)");
            }
            var msg = SomeIpMessage.TryDecode(recv.Buffer);
            if (IsMatch(msg, message)) return msg!;
        }
    }

    private static async Task<SomeIpMessage> RequestOverTcpAsync(
        string host, int port, SomeIpMessage message, int timeoutMs, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        using var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(host, port, timeoutCts.Token);
            var stream = tcp.GetStream();
            await stream.WriteAsync(message.Encode(), timeoutCts.Token);

            while (true)
            {
                var msg = await ReceiveTcpMessageAsync(stream, timeoutCts.Token);
                if (IsMatch(msg, message)) return msg;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"TCP 连接或等待 SOME/IP 响应超时({timeoutMs}ms)");
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
