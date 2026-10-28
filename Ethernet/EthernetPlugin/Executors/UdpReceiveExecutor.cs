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
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            if (setting.ExpectedLength is < 0 or > 65507) throw new ArgumentOutOfRangeException(nameof(setting.ExpectedLength), "UDP 期望长度必须在 0~65507 字节内");
            if (!Enum.IsDefined(setting.Encoding) || !Enum.IsDefined(setting.BindMode)) throw new InvalidOperationException("UDP 编码或绑定模式无效");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            timeoutMs = setting.TimeoutMs; localPort = setting.LocalPort;
            var bindAddress = setting.BindMode == UdpBindMode.AnyInterface
                ? IPAddress.Any
                : IPAddress.Loopback;

            using var udp = await TransientNetworkOperation<UdpClient>.OpenAsync(context.Resources,
                () => _bind(new IPEndPoint(bindAddress, setting.LocalPort)), setting.LocalPort,
                setting.TimeoutMs, cancellationToken, CheckExecution);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            var result = await udp.RunAsync((socket, token) => socket.ReceiveAsync(token).AsTask(), cts.Token, CheckExecution);
            var bytes = result.Buffer;
            if (setting.ExpectedLength > 0 && bytes.Length != setting.ExpectedLength)
                throw new InvalidDataException($"UDP 报文长度不匹配：实际 {bytes.Length} 字节，期望 {setting.ExpectedLength} 字节；未发布结果，不拼接或截断数据报");

            var resultStr = EthernetDataHelper.Decode(bytes, setting.Encoding);

            context.Log($"UDP 接收: 端口 {setting.LocalPort} 收到 {bytes.Length} 字节 来自 {result.RemoteEndPoint} [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, resultStr);

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
