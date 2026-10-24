using PluginCommon;
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
    private readonly Func<string, IExecutionContext, Task<string>> _evaluate;
    public SerialPortWriteExecutor() : this((expression, context) => Evaluator.EvalStringAsync(expression, context)) { }
    internal SerialPortWriteExecutor(Func<string, IExecutionContext, Task<string>> evaluate) => _evaluate = evaluate;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortWritePlugin().CreateSerializer();
            var s = (SerialPortWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var portName = await _evaluate(s.PortName, context);
            epoch.ThrowIfExpired();

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

			if (!context.Resources.TryGet<SerialConnectionResource>(key, out var port) || !port.IsOpen)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"串口 {portName} 未打开，请先执行 SerialPort_Open" }
                    }
                };

            var writeData = await _evaluate(s.WriteData, context);
            epoch.ThrowIfExpired();
            if (writeData.Length == 0) throw new ArgumentException("串口写入数据求值结果不能为空");
            var bytes = SerialPortHelper.ConvertToBytes(writeData, s.DataFormat);

			epoch.ThrowIfExpired();
			await port.WriteAsync(bytes, cancellationToken, epoch.ThrowIfExpired);

            context.Log($"串口 {portName} 写入 {bytes.Length} 字节 ({s.DataFormat})");
            epoch.ThrowIfExpired();
            SerialResultPublisher.EnsureActive(context, key, port, cancellationToken);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"{bytes.Length} bytes"
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
                    Error = ErrorInfo.FromException(ex, $"串口写入失败（含设置解析）: {ex.Message}")
                }
            };
        }
    }
}
