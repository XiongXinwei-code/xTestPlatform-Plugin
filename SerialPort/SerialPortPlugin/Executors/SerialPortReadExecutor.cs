using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Executors;

public sealed class SerialPortReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SerialPortReadPlugin().CreateSerializer();
        var s = (SerialPortReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
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

            var key = SerialPortHelper.GetPortKey(portName);

            if (!context.Resources.TryGet<SysSerialPort>(key, out var port) || !port.IsOpen)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"串口 {portName} 未打开，请先执行 SerialPort_Open" }
                    }
                };

            byte[] buffer;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(s.ReadTimeoutMs);

            if (s.ReadBytes > 0)
            {
                buffer = new byte[s.ReadBytes];
                int totalRead = 0;
                while (totalRead < s.ReadBytes)
                {
                    int read = await port.BaseStream.ReadAsync(
                        buffer, totalRead, s.ReadBytes - totalRead, cts.Token);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead < s.ReadBytes)
                    Array.Resize(ref buffer, totalRead);
            }
            else
            {
                using var ms = new MemoryStream();
                var temp = new byte[1024];
                var terminator = SerialPortHelper.NormalizeTerminator(s.Terminator);
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        int read = await port.BaseStream.ReadAsync(temp, 0, temp.Length, cts.Token);
                        if (read == 0) break;
                        ms.Write(temp, 0, read);

                        if (s.DataFormat == SerialPortDataFormat.String && !string.IsNullOrEmpty(terminator))
                        {
                            var current = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                            if (current.Contains(terminator))
                                break;
                        }
                    }
                }
                catch (OperationCanceledException) { /* timeout, return what we have */ }
                buffer = ms.ToArray();
            }

            var result = SerialPortHelper.ConvertFromBytes(buffer, s.DataFormat);

            if (!string.IsNullOrWhiteSpace(s.ResultVariable))
                context.SetVariable(s.ResultVariable, result);

            context.LogAction?.Invoke($"串口 {portName} 读取 {buffer.Length} 字节 ({s.DataFormat})");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = result
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"串口读取超时({s.ReadTimeoutMs}ms): 未读满 {s.ReadBytes} 字节" }
                }
            };
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
