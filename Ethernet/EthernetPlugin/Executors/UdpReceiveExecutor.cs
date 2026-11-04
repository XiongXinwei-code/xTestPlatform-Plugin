using System.Net;
using System.Net.Sockets;
using Ethernet.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class UdpReceiveExecutor : IStepExecutor
{
    private readonly Func<IPEndPoint, UdpClient> _bind;
    public UdpReceiveExecutor() : this(endpoint => new UdpClient(endpoint)) { }
    internal UdpReceiveExecutor(Func<IPEndPoint, UdpClient> bind) => _bind = bind;
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        int timeoutMs = 0, localPort = 0;
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
            var serializer = new UdpReceivePlugin().CreateSerializer();
            var setting = (UdpReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            EthernetStringOutput.Validate(context, setting.ResultVariable, CheckExecution);
            EthernetStringOutput.Validate(context, setting.RemoteEndpointVariable, CheckExecution);
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable) && setting.ResultVariable == setting.RemoteEndpointVariable)
                throw new ArgumentException("结果变量与来源端点变量不能使用相同路径");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            if (setting.ExpectedLength is < 0 or > 65507) throw new ArgumentOutOfRangeException(nameof(setting.ExpectedLength), "UDP 期望长度必须在 0~65507 字节内");
            if (!Enum.IsDefined(setting.Encoding) || (!setting.UseConnection && !Enum.IsDefined(setting.BindMode))) throw new InvalidOperationException("UDP 编码或绑定模式无效");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            timeoutMs = setting.TimeoutMs; localPort = setting.LocalPort;
            using var operation = new EthernetOperationBudget(setting.TimeoutMs, "UDP 接收", cancellationToken, CheckExecution);
            UdpReceiveResult result;
            UdpConnectionResource? connection = null;
            string? name = null;
            if (setting.UseConnection)
            {
                name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context); operation.Check();
                connection = UdpConnectionManager.Get(name!, context.Resources);
                localPort = connection.LocalEndpoint.Port;
                result = await connection.ReceiveAsync(operation.Token, operation.Check);
            }
            else
            {
                if (setting.LocalPort is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(setting.LocalPort));
                var bindAddress = setting.BindMode == UdpBindMode.AnyInterface ? IPAddress.Any : IPAddress.Loopback;
                using var udp = await TransientNetworkOperation<UdpClient>.OpenAsync(context.Resources,
                    () => _bind(new IPEndPoint(bindAddress, setting.LocalPort)), setting.LocalPort,
                    operation.RemainingMs, operation.Token, operation.Check);
                result = await udp.RunAsync((socket, token) => socket.ReceiveAsync(token).AsTask(), operation.Token, operation.Check);
            }
            operation.Check(); // 通信预算止于完整数据报接收；日志回调不是接收等待。
            void CheckPublication()
            {
                CheckExecution();
                if (connection != null)
                {
                    connection.CheckOpen();
                    if (!ReferenceEquals(connection, UdpConnectionManager.Get(name!, context.Resources)))
                        throw new InvalidOperationException("UDP 连接已替换，拒绝发布旧代次响应");
                }
                CheckExecution();
            }
            CheckPublication();
            var bytes = result.Buffer;
            if (setting.ExpectedLength > 0 && bytes.Length != setting.ExpectedLength)
                throw new InvalidDataException($"UDP 报文长度不匹配：实际 {bytes.Length} 字节，期望 {setting.ExpectedLength} 字节；未发布结果，不拼接或截断数据报");

            var resultStr = EthernetDataHelper.Decode(bytes, setting.Encoding);

            context.Log($"UDP 接收: 端口 {localPort} 收到 {bytes.Length} 字节 来自 {result.RemoteEndPoint} [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");
            CheckPublication();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, resultStr);

            CheckPublication();
            if (!string.IsNullOrWhiteSpace(setting.RemoteEndpointVariable)) context.SetVariable(setting.RemoteEndpointVariable, result.RemoteEndPoint.ToString());

            CheckPublication();
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = resultStr
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
                    Error = ErrorInfo.FromException(ex, $"UDP 接收超时({timeoutMs}ms): 端口 {localPort}")
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
                    Error = ErrorInfo.FromException(ex, $"UDP 接收失败: {ex.Message}")
                }
            };
        }
    }
}
