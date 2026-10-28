using System.Diagnostics;
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
            long startedAt = Stopwatch.GetTimestamp();
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortClosePlugin().CreateSerializer();
            var s = (SerialPortCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (s.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(s.TimeoutMs));
            int Remaining()
            {
                epoch.ThrowIfExpired();
                return SerialPortHelper.RemainingBudget(startedAt, s.TimeoutMs);
            }
            Remaining();

            var portName = await Evaluator.EvalStringAsync(s.PortName, context);
            Remaining();

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
                Remaining();
                SerialPortHelper.MarkCloseRequested(context.Resources, key);
            }
            if (port != null)
            {
                await port.CloseAsync(Remaining(), cancellationToken);
                Remaining();
                lock (SerialPortHelper.RegistryLock(context.Resources))
                {
                    bool found = context.Resources.TryGet<SerialConnectionResource>(key, out var current);
                    Remaining();
                    if (found && ReferenceEquals(current, port))
                        context.Resources.Remove(key);
                }
                Remaining();
                if (port.CloseWarning is { } warning)
                    context.Log(LogLevel.Warn, $"串口 {portName} 已释放；保留已退出的中断或取消回调诊断", warning.ToString());
                Remaining();
                context.Log($"串口 {portName} 已关闭");
                Remaining();
            }
            else
            {
                context.Log(LogLevel.Warn, $"串口 {portName} 未找到或已关闭");
                Remaining();
            }

            Remaining();
            SerialResultPublisher.EnsureCallerActive(context, cancellationToken);
            Remaining();
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
