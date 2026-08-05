using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using SysSerialPort = System.IO.Ports.SerialPort;
using SysStopBits = System.IO.Ports.StopBits;
using SysParity = System.IO.Ports.Parity;

namespace SerialPort.Executors;

public sealed class SerialPortOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortOpenPlugin().CreateSerializer();
            var s = (SerialPortOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var portName = await Evaluator.EvalStringAsync(s.PortName, context);

            if (string.IsNullOrWhiteSpace(portName))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "端口名称为空" }
                    }
                };

            var port = new SysSerialPort
            {
                PortName = portName,
                BaudRate = s.BaudRate,
                DataBits = s.DataBits,
                StopBits = (SysStopBits)s.StopBits,
                Parity = (SysParity)s.Parity,
                ReadTimeout = s.ReadTimeoutMs,
                WriteTimeout = s.WriteTimeoutMs
            };

            port.Open();

            var key = SerialPortHelper.GetPortKey(portName);

            // 若已存在同名串口（序列异常终止未关闭），先关闭销毁
            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var existing) && existing is SysSerialPort oldPort)
            {
                try { if (oldPort.IsOpen) oldPort.Close(); } catch { /* 忽略关闭异常 */ }
                try { oldPort.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"串口 {portName} 检测到已有连接，已自动关闭旧连接");
            }

            context.CurrentStep.RuntimeData[key] = port;

            context.LogAction?.Invoke($"串口 {portName} 已打开 (波特率: {s.BaudRate})");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = portName
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
