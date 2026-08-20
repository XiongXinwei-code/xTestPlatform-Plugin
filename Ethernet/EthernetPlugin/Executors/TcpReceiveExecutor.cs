using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpReceiveExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new TcpReceivePlugin().CreateSerializer();
        var setting = (TcpReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);

            var client = TcpConnectionManager.Get(name!);
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
                    var n = await stream.ReadAsync(buffer.AsMemory(received, setting.ExpectedLength - received), cts.Token);
                    if (n == 0) break;
                    received += n;
                }
                if (received < setting.ExpectedLength)
                    buffer = buffer[..received];
            }
            else
            {
                buffer = new byte[65535];
                var n = await stream.ReadAsync(buffer, cts.Token);
                buffer = buffer[..n];
            }

            var result = EthernetDataHelper.Decode(buffer, setting.Encoding);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"TCP 接收: {name} 收到 {buffer.Length} 字节 [{EthernetDataHelper.Decode(buffer, EthernetDataEncoding.Hex)}]");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, result);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = result
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
                    Error = new ErrorInfo { Message = $"TCP 接收超时({setting.TimeoutMs}ms)" }
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
                    Error = ErrorInfo.FromException(ex, $"TCP RECEIVE 失败: {ex.Message}")
                }
            };
        }
    }
}
