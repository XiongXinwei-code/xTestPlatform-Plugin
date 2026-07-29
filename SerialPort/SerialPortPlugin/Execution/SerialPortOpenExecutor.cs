using System.IO.Ports;
using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.Services;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Execution;

public sealed class SerialPortOpenExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SerialPortOpenPlugin().CreateSerializer();
        var s = (SerialPortOpenSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);

        try
        {
            if (string.IsNullOrWhiteSpace(s.PortName))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "端口名不能为空" }
                    }
                };

            context.LogAction?.Invoke($"正在打开串口 {s.PortName}，波特率 {s.BaudRate}...");

            SerialPortManager.Open(
                s.PortName,
                s.BaudRate,
                s.DataBits,
                (StopBits)s.StopBits,
                (Parity)s.Parity,
                s.ReadTimeoutMs,
                s.WriteTimeoutMs);

            context.LogAction?.Invoke($"串口 {s.PortName} 已打开");

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
                    Error = new ErrorInfo { Message = $"打开串口 {s.PortName} 失败: {ex.Message}" }
                }
            };
        }
    }
}
