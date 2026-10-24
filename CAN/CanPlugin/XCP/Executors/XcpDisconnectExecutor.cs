using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpDisconnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new XcpDisconnectPlugin().CreateSerializer();
            var setting = (XcpDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            await guard.RunAsync(() => client.DisconnectAsync(cancellationToken));

            guard.Log("XCP DISCONNECT 成功");

            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed } };
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
                    Error  = ErrorInfo.FromException(ex, $"XCP DISCONNECT 失败: {ex.Message}")
                }
            };
        }
    }
}
