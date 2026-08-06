using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpCloseExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new TcpClosePlugin().CreateSerializer();
        var setting = (TcpCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);

            TcpConnectionManager.Close(name!);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"TCP 连接已关闭: {name}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"TCP 已关闭: {name}"
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
                    Error = new ErrorInfo { Message = $"TCP CLOSE 失败: {ex.Message}" }
                }
            };
        }
    }
}
