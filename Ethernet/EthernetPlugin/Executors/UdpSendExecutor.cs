using System.Net;
using System.Net.Sockets;
using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class UdpSendExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdpSendPlugin().CreateSerializer();
        var setting = (UdpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.Data, context);

            if (!int.TryParse(portStr, out var port))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"UDP 发送失败: 端口号 '{portStr}' 无效" }
                    }
                };

            var bytes = EthernetDataHelper.Encode(dataStr!, setting.Encoding);

            using var udp = new UdpClient(setting.LocalPort);
            await udp.SendAsync(bytes, new IPEndPoint(IPAddress.Parse(host!), port), cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"UDP 发送: {host}:{port} 发送 {bytes.Length} 字节 [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"UDP 已发送 {bytes.Length} 字节 -> {host}:{port}"
                }
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
                    Error = ErrorInfo.FromException(ex, $"UDP SEND 失败: {ex.Message}")
                }
            };
        }
    }
}
