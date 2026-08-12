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
        var step = context.CurrentStep!.Step;
        var serializer = new DoipVehicleDiscoveryPlugin().CreateSerializer();
        var setting = (DoipVehicleDiscoverySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var broadcast = await EthernetExecutorHelper.EvalStringAsync(setting.BroadcastAddress, context);

            // 车辆识别请求：8 字节头，无负载
            var request = new byte[8];
            request[0] = ProtocolVersion;
            request[1] = unchecked((byte)~ProtocolVersion);
            request[2] = (byte)(PtVehicleIdentRequest >> 8);
            request[3] = (byte)PtVehicleIdentRequest;

            using var udp = new UdpClient { EnableBroadcast = true };
            await udp.SendAsync(request, new IPEndPoint(IPAddress.Parse(broadcast), setting.Port), cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"DoIP 车辆识别请求已广播: {broadcast}:{setting.Port}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            var result = await udp.ReceiveAsync(cts.Token);
            var data = result.Buffer;

            if (data.Length < 8 + 32)
                throw new InvalidOperationException($"车辆公告报文长度不足: {data.Length} 字节");

            var payloadType = (ushort)((data[2] << 8) | data[3]);
            if (payloadType != PtVehicleAnnouncement)
                throw new InvalidOperationException($"收到意外的 PayloadType 0x{payloadType:X4}");

            // 负载: VIN(17) + LogicalAddress(2) + EID(6) + GID(6) + FurtherAction(1)
            var vin = Encoding.ASCII.GetString(data, 8, 17).TrimEnd('\0');
            var logicalAddress = (ushort)((data[25] << 8) | data[26]);
            var summary = $"VIN={vin}, LogicalAddress=0x{logicalAddress:X4}, IP={result.RemoteEndPoint.Address}";

            if (setting.EnableLog)
                context.LogAction?.Invoke($"DoIP 发现车辆: {summary}");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, summary);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = summary
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"DoIP 车辆发现超时({setting.TimeoutMs}ms): 未收到车辆公告" }
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
                    Error = new ErrorInfo { Message = $"DoIP 车辆发现失败: {ex.Message}" }
                }
            };
        }
    }
}
