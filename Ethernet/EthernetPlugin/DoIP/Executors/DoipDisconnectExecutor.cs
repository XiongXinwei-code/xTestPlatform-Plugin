using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipDisconnectExecutor : IStepExecutor
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
            var serializer = new DoipDisconnectPlugin().CreateSerializer();
            var setting = (DoipDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.SessionName, context);
            CheckExecution();

            await DoipConnectionManager.CloseAsync(name, cancellationToken, context.Resources, setting.TimeoutMs, CheckExecution);

            context.Log($"DoIP 会话已断开: {name}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"DoIP 已断开: {name}"
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
                    Error = ErrorInfo.FromException(ex, $"DoIP DISCONNECT 失败: {ex.Message}")
                }
            };
        }
    }
}
