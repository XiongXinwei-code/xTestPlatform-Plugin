using System.Net;
using System.Net.Sockets;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP.Executors;

public sealed class SomeIpSubscribeExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SomeIpSubscribePlugin().CreateSerializer();
        var setting = (SomeIpSubscribeSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.LocalPort, context);
            var serviceStr = await EthernetExecutorHelper.EvalStringAsync(setting.ServiceId, context);
            var eventStr = await EthernetExecutorHelper.EvalStringAsync(setting.EventId, context);

            var localPort = SomeIpHelper.ParsePort(portStr, "LocalPort");
            var serviceId = SomeIpHelper.ParseId(serviceStr, "ServiceId");
            var eventId = SomeIpHelper.ParseId(eventStr, "EventId");

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"SOME/IP 订阅监听: 端口 {localPort} Service=0x{serviceId:X4} Event=0x{eventId:X4}，超时 {setting.TimeoutMs}ms");

            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, localPort));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(setting.TimeoutMs);

            SomeIpMessage? notification = null;
            while (notification == null)
            {
                UdpReceiveResult recv;
                try
                {
                    recv = await udp.ReceiveAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"等待 SOME/IP 事件通知超时({setting.TimeoutMs}ms)");
                }
                var msg = SomeIpMessage.TryDecode(recv.Buffer);
                if (msg != null && msg.ServiceId == serviceId && msg.MethodId == eventId
                    && msg.MessageType == SomeIpMessageType.Notification)
                    notification = msg;
            }

            var payloadHex = SomeIpHelper.ToHex(notification.Payload);
            if (setting.EnableLog)
                context.LogAction?.Invoke($"SOME/IP 收到事件通知: [{payloadHex}]");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, payloadHex);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = payloadHex }
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
                    Error = ErrorInfo.FromException(ex, $"SOME/IP 订阅失败: {ex.Message}")
                }
            };
        }
    }
}
