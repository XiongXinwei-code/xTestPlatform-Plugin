using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpSdDiscoverExecutor : IStepExecutor
{
    private const ushort SdServiceId = 0xFFFF;
    private const ushort SdMethodId = 0x8100;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SomeIpSdDiscoverPlugin().CreateSerializer();
        var setting = (SomeIpSdDiscoverSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var addrStr = await EthernetExecutorHelper.EvalStringAsync(setting.MulticastAddress, context);
            var serviceStr = await EthernetExecutorHelper.EvalStringAsync(setting.ServiceId, context);

            var multicast = IPAddress.Parse(addrStr.Trim());
            var findServiceId = SomeIpHelper.ParseId(serviceStr, "ServiceId");

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"SOME/IP-SD 发现: {multicast}:{setting.Port} 查找 Service=0x{findServiceId:X4}，超时 {setting.TimeoutMs}ms");

            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, setting.Port));
            udp.JoinMulticastGroup(multicast);

            // 发送 FindService
            var find = BuildFindService(findServiceId);
            await udp.SendAsync(find, new IPEndPoint(multicast, setting.Port), cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(setting.TimeoutMs);

            var offers = new List<string>();
            try
            {
                while (true)
                {
                    var recv = await udp.ReceiveAsync(timeoutCts.Token);
                    foreach (var offer in ParseOffers(recv.Buffer, findServiceId, recv.RemoteEndPoint))
                        if (!offers.Contains(offer)) offers.Add(offer);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时结束收集
            }

            if (offers.Count == 0)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Error = new ErrorInfo { Message = $"未发现服务 0x{findServiceId:X4}（{setting.TimeoutMs}ms 内无 OfferService 响应）" }
                    }
                };

            var result = string.Join("; ", offers);
            if (setting.EnableLog)
                context.LogAction?.Invoke($"SOME/IP-SD 发现服务: {result}");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, result);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = result }
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
                    Error = new ErrorInfo { Message = $"SOME/IP-SD 服务发现失败: {ex.Message}" }
                }
            };
        }
    }

    /// <summary>构造 SD FindService 报文（Type=0x00 Entry，TTL=3s）。</summary>
    private static byte[] BuildFindService(ushort serviceId)
    {
        // SD Payload: Flags(1)+Reserved(3) + EntriesLength(4) + Entry(16) + OptionsLength(4)
        var payload = new byte[4 + 4 + 16 + 4];
        payload[0] = 0x80; // Reboot flag
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), 16);
        var e = payload.AsSpan(8);
        e[0] = 0x00; // Type: FindService
        e[1] = 0x00; // Index1
        e[2] = 0x00; // Index2
        e[3] = 0x00; // #opt
        BinaryPrimitives.WriteUInt16BigEndian(e[4..], serviceId);
        BinaryPrimitives.WriteUInt16BigEndian(e[6..], 0xFFFF); // InstanceID: any
        e[8] = 0xFF; // MajorVersion: any
        e[9] = 0x00; e[10] = 0x00; e[11] = 0x03; // TTL = 3
        BinaryPrimitives.WriteUInt32BigEndian(e[12..], 0xFFFFFFFF); // MinorVersion: any
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(24), 0); // OptionsLength

        var msg = new SomeIpMessage
        {
            ServiceId        = SdServiceId,
            MethodId         = SdMethodId,
            ClientId         = 0x0000,
            SessionId        = 0x0001,
            InterfaceVersion = 0x01,
            MessageType      = SomeIpMessageType.Notification,
            Payload          = payload,
        };
        return msg.Encode();
    }

    /// <summary>解析 SD 报文中的 OfferService Entry，并解析引用的 IPv4 Endpoint 选项。</summary>
    private static List<string> ParseOffers(byte[] data, ushort findServiceId, IPEndPoint from)
    {
        var offers = new List<string>();
        var msg = SomeIpMessage.TryDecode(data);
        if (msg == null || msg.ServiceId != SdServiceId || msg.MethodId != SdMethodId) return offers;
        var p = msg.Payload;
        if (p.Length < 8) return offers;

        var entriesLength = (int)BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(4));
        var entriesStart = 8;
        var entriesEnd = Math.Min(entriesStart + entriesLength, p.Length);

        // 解析 Options 区（位于 Entries 之后：OptionsLength(4) + Options）
        var options = ParseOptions(p, entriesEnd);

        var offset = entriesStart;
        while (offset + 16 <= entriesEnd)
        {
            var type = p[offset];
            var index1 = p[offset + 1];
            var optCounts = p[offset + 3];
            var num1 = (optCounts >> 4) & 0x0F;
            var serviceId = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(offset + 4));
            var instanceId = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(offset + 6));
            var major = p[offset + 8];
            var ttl = (uint)((p[offset + 9] << 16) | (p[offset + 10] << 8) | p[offset + 11]);

            // Type 0x01 = OfferService，且 TTL>0
            if (type == 0x01 && ttl > 0 &&
                (findServiceId == 0xFFFF || serviceId == findServiceId))
            {
                // 通过 Index1/Num1 关联该 Entry 引用的 Endpoint 选项
                var endpoints = new List<string>();
                for (int i = index1; i < index1 + num1 && i < options.Count; i++)
                    if (options[i] != null) endpoints.Add(options[i]!);

                var endpointInfo = endpoints.Count > 0
                    ? string.Join(",", endpoints)
                    : $"{from.Address}(SD来源)";
                offers.Add($"Service=0x{serviceId:X4} Instance=0x{instanceId:X4} Ver={major} @ {endpointInfo}");
            }
            offset += 16;
        }
        return offers;
    }

    /// <summary>解析 SD Options 区，返回按索引排列的选项描述（仅支持 IPv4 Endpoint 0x04，其他选项占位 null）。</summary>
    private static List<string?> ParseOptions(byte[] p, int entriesEnd)
    {
        var options = new List<string?>();
        if (entriesEnd + 4 > p.Length) return options;

        var optionsLength = (int)BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(entriesEnd));
        var offset = entriesEnd + 4;
        var end = Math.Min(offset + optionsLength, p.Length);

        while (offset + 3 <= end)
        {
            // Option: Length(2, 不含 Length 和 Type 字段) + Type(1) + 内容
            var optLen = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(offset));
            var optType = p[offset + 2];
            var contentStart = offset + 3;
            var optTotal = 3 + optLen;
            if (offset + optTotal > end) break;

            // IPv4 Endpoint Option: Reserved(1) + IPv4(4) + Reserved(1) + Protocol(1) + Port(2)
            if (optType == 0x04 && optLen >= 9)
            {
                var ip = new IPAddress(p.AsSpan(contentStart + 1, 4).ToArray());
                var proto = p[contentStart + 6] switch { 0x06 => "TCP", 0x11 => "UDP", var b => $"0x{b:X2}" };
                var port = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(contentStart + 7));
                options.Add($"{ip}:{port}/{proto}");
            }
            else
            {
                options.Add(null); // 不支持的选项类型，占位保持索引对齐
            }
            offset += optTotal;
        }
        return options;
    }
}
