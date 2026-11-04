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

            if (!int.TryParse(portStr, out var port) || port is < 1 or > 65535)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"UDP 发送失败: 端口号 '{portStr}' 无效" }
                    }
                };

            var bytes = EthernetDataHelper.Encode(dataStr!, setting.Encoding);

            if (!Enum.IsDefined(setting.Encoding)) throw new ArgumentException("UDP 编码无效");
            if (bytes.Length > 65507) throw new ArgumentException("UDP 数据不能超过 65507 字节");
            using var operation = budget = new EthernetOperationBudget(setting.SendTimeoutMs, "UDP 发送", cancellationToken, CheckExecution);
            Action? checkConnection = null;
            if (setting.UseConnection)
            {
                var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context); operation.Check();
                var connection = UdpConnectionManager.Get(name!, context.Resources);
                checkConnection = () =>
                {
                    CheckExecution(); connection.CheckOpen();
                    if (!ReferenceEquals(connection, UdpConnectionManager.Get(name!, context.Resources)))
                        throw new InvalidOperationException("UDP 连接已替换，旧代次发送不能报告成功");
                };
                await connection.SendAsync(_network, host!, port, bytes, operation);
                operation.Check();
                if (!ReferenceEquals(connection, UdpConnectionManager.Get(name!, context.Resources)))
                    throw new InvalidOperationException("UDP 连接已替换，旧代次发送不能报告成功");
            }
            else
            {
                if (setting.LocalPort is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(setting.LocalPort));
                using var udp = await TransientNetworkOperation<UdpClient>.OpenAsync(context.Resources,
                    () => new UdpClient(setting.LocalPort) { EnableBroadcast = setting.EnableBroadcast }, setting.LocalPort,
                    operation.RemainingMs, operation.Token, operation.Check);
                await udp.RunAsync(async (socket, token) =>
                {
                    var endpoint = await EthernetNetworkIo.ResolveEndpointAsync(_network, host!, port,
                        socket.Client.AddressFamily, operation, token);
                    operation.BeforeSubmission();
                    await _network.SendUdpAsync(socket, endpoint, bytes, token);
                    operation.Check();
                }, operation.Token, operation.Check);
            }

            checkConnection?.Invoke();
            context.Log($"UDP 发送: {host}:{port} 发送 {bytes.Length} 字节 [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            CheckExecution(); checkConnection?.Invoke();
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
