using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpReceiveExecutor : IStepExecutor
{
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
            var serializer = new TcpReceivePlugin().CreateSerializer();
            var setting = (TcpReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            ArgumentOutOfRangeException.ThrowIfNegative(setting.ExpectedLength);
            if (!Enum.IsDefined(setting.Encoding)) throw new InvalidOperationException("TCP 接收编码无效");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            timeoutMs = setting.TimeoutMs;
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);
            CheckExecution();

            var client = TcpConnectionManager.Get(name!, context.Resources);
            var stream = client.GetStream();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            byte[] buffer;
            if (setting.ExpectedLength > 0)
            {
                buffer = new byte[setting.ExpectedLength];
                var received = 0;
                while (received < setting.ExpectedLength)
                {
                    CheckExecution();
                    var n = await stream.ReadAsync(buffer.AsMemory(received, setting.ExpectedLength - received), cts.Token);
                    if (n == 0) break;
                    received += n;
                }
                if (received < setting.ExpectedLength)
                {
                    return new ExecutionResult
                    {
                        StepResult = new StepResult
                        {
                            Status = TestStatus.Error,
                            Error = new ErrorInfo { Message = $"TCP 接收失败: 对端已关闭连接，仅收到 {received}/{setting.ExpectedLength} 字节" }
                        }
                    };
                }
            }
            else
            {
                buffer = new byte[65535];
                CheckExecution();
                var n = await stream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                {
                    return new ExecutionResult
                    {
                        StepResult = new StepResult
                        {
                            Status = TestStatus.Error,
                            Error = new ErrorInfo { Message = "TCP 接收失败: 对端已关闭连接，未收到数据" }
                        }
                    };
                }
                buffer = buffer[..n];
            }

            var result = EthernetDataHelper.Decode(buffer, setting.Encoding);

            context.Log($"TCP 接收: {name} 收到 {buffer.Length} 字节 [{EthernetDataHelper.Decode(buffer, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (!ReferenceEquals(client, TcpConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("TCP 连接已被替换，拒绝发布旧代次响应");

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, result);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = result
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
                    Error = ErrorInfo.FromException(ex, $"TCP 接收超时({timeoutMs}ms)")
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
                    Error = ErrorInfo.FromException(ex, $"TCP 接收失败: {ex.Message}")
                }
            };
        }
    }
}
