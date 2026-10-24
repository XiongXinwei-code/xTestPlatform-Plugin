using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpSendExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

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
            var serializer = new TcpSendPlugin().CreateSerializer();
            var setting = (TcpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.SendTimeoutMs);
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);
            CheckExecution();
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.Data, context);
            CheckExecution();

            var client = TcpConnectionManager.Get(name!, context.Resources);
            var bytes = EthernetDataHelper.Encode(dataStr!, setting.Encoding);
            var stream = client.GetStream();

            // 对端不取数据导致发送缓冲区满时，WriteAsync 会一直等待，需限定超时
            var timeoutMs = setting.SendTimeoutMs;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                CheckExecution();
                await stream.WriteAsync(bytes, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"TCP 发送超时({timeoutMs}ms): 对端未接收数据");
            }

            context.Log($"TCP 发送: {name} 发送 {bytes.Length} 字节 [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (!ReferenceEquals(client, TcpConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("TCP 连接已关闭或被替换；旧代次发送已完成，不自动重发");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"已发送 {bytes.Length} 字节"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex)
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
                    Error = ErrorInfo.FromException(ex, $"TCP 发送失败: {ex.Message}")
                }
            };
        }
    }
}
