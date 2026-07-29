using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.Services;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Execution;

public sealed class SerialPortCloseExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SerialPortClosePlugin().CreateSerializer();
        var s = (SerialPortCloseSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);

        try
        {
            context.LogAction?.Invoke($"正在关闭串口 {s.PortName}...");
            SerialPortManager.Close(s.PortName);
            context.LogAction?.Invoke($"串口 {s.PortName} 已关闭");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = s.PortName
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Aborted }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"关闭串口失败: {ex.Message}" }
                }
            };
        }
    }
}
