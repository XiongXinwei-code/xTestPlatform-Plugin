using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipDisconnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new DoipDisconnectPlugin().CreateSerializer();
        var setting = (DoipDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.SessionName, context);

            DoipConnectionManager.Close(name);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"DoIP 会话已断开: {name}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"DoIP 已断开: {name}"
                }
            };
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
                    Error = new ErrorInfo { Message = $"DoIP DISCONNECT 失败: {ex.Message}" }
                }
            };
        }
    }
}
