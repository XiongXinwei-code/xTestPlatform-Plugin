using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipVehicleDiscoveryExecutor : IStepExecutor
{
    private const byte ProtocolVersion = 0x02;
    private const ushort PtVehicleIdentRequest = 0x0001;
    private const ushort PtVehicleAnnouncement = 0x0004;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        int timeoutMs = 0;
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
            var serializer = new DoipVehicleDiscoveryPlugin().CreateSerializer();
            var setting = (DoipVehicleDiscoverySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            timeoutMs = setting.TimeoutMs;
            var broadcast = await EthernetExecutorHelper.EvalStringAsync(setting.BroadcastAddress, context);
            CheckExecution();

            // 车辆识别请求：8 字节头，无负载
            var request = new byte[8];
            request[0] = ProtocolVersion;
            request[1] = unchecked((byte)~ProtocolVersion);
            request[2] = (byte)(PtVehicleIdentRequest >> 8);
            request[3] = (byte)PtVehicleIdentRequest;

            using var udp = new UdpClient { EnableBroadcast = true };
            CheckExecution();
            await udp.SendAsync(request, new IPEndPoint(IPAddress.Parse(broadcast), setting.Port), cancellationToken);

            context.Log($"DoIP 车辆识别请求已广播: {broadcast}:{setting.Port}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            CheckExecution();
            var result = await udp.ReceiveAsync(cts.Token);
            var data = result.Buffer;

            if (data.Length < 8 + 32)
                throw new InvalidOperationException($"车辆公告报文长度不足: {data.Length} 字节");

            if (data[1] != unchecked((byte)~data[0]))
                throw new InvalidDataException("车辆公告协议版本与反码不匹配");
            var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4));
            if (payloadLength != (uint)(data.Length - 8))
                throw new InvalidDataException($"车辆公告声明负载长度 {payloadLength} 与实际 {data.Length - 8} 字节不一致");

            var payloadType = (ushort)((data[2] << 8) | data[3]);
            if (payloadType != PtVehicleAnnouncement)
                throw new InvalidOperationException($"收到意外的 PayloadType 0x{payloadType:X4}");

            // 负载: VIN(17) + LogicalAddress(2) + EID(6) + GID(6) + FurtherAction(1)
            var vin = Encoding.ASCII.GetString(data, 8, 17).TrimEnd('\0');
            var logicalAddress = (ushort)((data[25] << 8) | data[26]);
            var summary = $"VIN={vin}, LogicalAddress=0x{logicalAddress:X4}, IP={result.RemoteEndPoint.Address}";

            context.Log($"DoIP 发现车辆: {summary}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, summary);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = summary
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"DoIP 车辆发现超时({timeoutMs}ms): 未收到车辆公告")
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"DoIP 车辆发现失败: {ex.Message}")
                }
            };
        }
    }
}
