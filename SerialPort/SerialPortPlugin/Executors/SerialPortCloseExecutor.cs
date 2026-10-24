using PluginCommon;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Executors;

public sealed class SerialPortCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortClosePlugin().CreateSerializer();
            var s = (SerialPortCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (s.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(s.TimeoutMs));

            var portName = await Evaluator.EvalStringAsync(s.PortName, context);
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

            SerialConnectionResource? port;
            lock (SerialPortHelper.RegistryLock(context.Resources))
            {
                context.Resources.TryGet<SerialConnectionResource>(key, out port);
                epoch.ThrowIfExpired();
                SerialPortHelper.MarkCloseRequested(context.Resources, key);
            }
            if (port != null)
            {
                epoch.ThrowIfExpired();
                await port.CloseAsync(s.TimeoutMs, cancellationToken);
                epoch.ThrowIfExpired();
                lock (SerialPortHelper.RegistryLock(context.Resources))
                {
                    if (context.Resources.TryGet<SerialConnectionResource>(key, out var current) && ReferenceEquals(current, port))
                        context.Resources.Remove(key);
                }
                context.Log($"串口 {portName} 已关闭");
                epoch.ThrowIfExpired();
            }
            else
            {
                context.Log(LogLevel.Warn, $"串口 {portName} 未找到或已关闭");
                epoch.ThrowIfExpired();
            }

            epoch.ThrowIfExpired();
            SerialResultPublisher.EnsureCallerActive(context, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = portName
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
                    Error = ErrorInfo.FromException(ex, $"串口关闭失败（含设置解析）: {ex.Message}")
                }
            };
        }
    }
}
