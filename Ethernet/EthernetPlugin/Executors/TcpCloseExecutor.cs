using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpCloseExecutor : IStepExecutor
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
            var serializer = new TcpClosePlugin().CreateSerializer();
            var setting = (TcpCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);
            CheckExecution();

            await TcpConnectionManager.CloseAsync(name!, cancellationToken, context.Resources, setting.TimeoutMs, CheckExecution);

            context.Log($"TCP 连接已关闭: {name}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"TCP 已关闭: {name}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error = ErrorInfo.FromException(ex, $"TCP CLOSE 失败: {ex.Message}")
                }
            };
        }
    }
}
