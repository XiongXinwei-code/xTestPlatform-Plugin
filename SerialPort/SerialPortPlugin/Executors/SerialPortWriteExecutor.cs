using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Executors;

public sealed class SerialPortWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortWritePlugin().CreateSerializer();
            var s = (SerialPortWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var portName = await Evaluator.EvaluateAsync<string>(s.PortName, context) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(portName))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "端口名称为空" }
                    }
                };

            var key = SerialPortHelper.GetPortKey(portName);

            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not SysSerialPort port || !port.IsOpen)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"串口 {portName} 未打开，请先执行 SerialPort_Open" }
                    }
                };

            var writeData = await Evaluator.EvaluateAsync<string>(s.WriteData, context) ?? string.Empty;
            var bytes = SerialPortHelper.ConvertToBytes(writeData, s.DataFormat);

            await port.BaseStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);

            context.LogAction?.Invoke($"串口 {portName} 写入 {bytes.Length} 字节 ({s.DataFormat})");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{bytes.Length} bytes"
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
                    Error = new ErrorInfo { Message = ex.Message }
                }
            };
        }
    }
}
