using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpSendExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new TcpSendPlugin().CreateSerializer();
        var setting = (TcpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.Data, context);

            var client = TcpConnectionManager.Get(name!);
            var bytes = EthernetDataHelper.Encode(dataStr!, setting.Encoding);
            var stream = client.GetStream();

            // 对端不取数据导致发送缓冲区满时，WriteAsync 会一直等待，需限定超时
            var timeoutMs = setting.SendTimeoutMs > 0 ? setting.SendTimeoutMs : 3000;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                await stream.WriteAsync(bytes, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"TCP 发送超时({timeoutMs}ms): 对端未接收数据");
            }

            if (setting.EnableLog)
                context.LogAction?.Invoke($"TCP 发送: {name} 发送 {bytes.Length} 字节 [{EthernetDataHelper.Decode(bytes, EthernetDataEncoding.Hex)}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"已发送 {bytes.Length} 字节"
                }
            };
        }
        catch (OperationCanceledException)
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
                    Error = ErrorInfo.FromException(ex, $"TCP SEND 失败: {ex.Message}")
                }
            };
        }
    }
}
