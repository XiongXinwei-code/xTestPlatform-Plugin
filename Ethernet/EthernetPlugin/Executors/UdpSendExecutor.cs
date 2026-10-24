using System.Net;
using System.Net.Sockets;
using Ethernet.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class UdpSendExecutor : IStepExecutor
{
    private readonly IEthernetNetworkIo _network;
    public UdpSendExecutor() : this(EthernetNetworkIo.Instance) { }
    internal UdpSendExecutor(IEthernetNetworkIo network) => _network = network;
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
            var serializer = new UdpSendPlugin().CreateSerializer();
            var setting = (UdpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (setting.SendTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.SendTimeoutMs), "发送总超时必须大于 0");
            var host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            CheckExecution();
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);
            CheckExecution();
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.Data, context);
            CheckExecution();

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

            var endpoint = new IPEndPoint(IPAddress.Parse(host!), port);
            using var operation = budget = new EthernetOperationBudget(setting.SendTimeoutMs, "UDP 发送", cancellationToken, CheckExecution);
            using var udp = new UdpClient(setting.LocalPort);
            operation.BeforeSubmission();
            await _network.SendUdpAsync(udp, endpoint, bytes, operation.Token);
            operation.Check();

            context.Log($"UDP 发送: {host}:{port} 发送 {bytes.Length} 字节 [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"UDP 已发送 {bytes.Length} 字节 -> {host}:{port}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted,
                Value = budget?.SubmissionStarted == true ? "已尝试发送，报文是否送达未知；未自动重发" : "本次尚未提交发送" } };
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
                    Error = ErrorInfo.FromException(ex, $"UDP SEND 失败: {ex.Message}")
                }
            };
        }
    }
}
