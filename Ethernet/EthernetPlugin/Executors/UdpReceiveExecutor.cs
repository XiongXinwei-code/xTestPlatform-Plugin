using System.Net;
using System.Net.Sockets;
using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class UdpReceiveExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdpReceivePlugin().CreateSerializer();
        var setting = (UdpReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var bindAddress = setting.BindMode == UdpBindMode.AnyInterface
                ? IPAddress.Any
                : IPAddress.Loopback;

            using var udp = new UdpClient(new IPEndPoint(bindAddress, setting.LocalPort));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            var result = await udp.ReceiveAsync(cts.Token);
            var bytes = result.Buffer;

            var resultStr = EthernetDataHelper.Decode(bytes, setting.Encoding);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"UDP 接收: 端口 {setting.LocalPort} 收到 {bytes.Length} 字节 来自 {result.RemoteEndPoint} [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, resultStr);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = resultStr
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
                    Error = new ErrorInfo { Message = $"UDP 接收超时({setting.TimeoutMs}ms): 端口 {setting.LocalPort}" }
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
                    Error = new ErrorInfo { Message = $"UDP RECEIVE 失败: {ex.Message}" }
                }
            };
        }
    }
}
