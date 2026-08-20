using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpDisconnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new XcpDisconnectPlugin().CreateSerializer();
        var setting = (XcpDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            await client.DisconnectAsync(cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke("XCP DISCONNECT 成功");

            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed } };
        }
        catch (OperationCanceledException)
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
