using System.Diagnostics;
using SerialPort.Helpers;
using SerialPort.Models;
using PluginCommon;
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
    private readonly Func<string, SerialPortOpenSetting, Action<Exception>, (SerialConnectionResource Owner, Action Open)> _create;
    public SerialPortOpenExecutor() : this(CreateNative) { }
    internal SerialPortOpenExecutor(Func<string, SerialPortOpenSetting, Action<Exception>, (SerialConnectionResource Owner, Action Open)> create) => _create = create;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        SerialConnectionResource? unpublished = null;
        try
        {
            long startedAt = Stopwatch.GetTimestamp();
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new SerialPortOpenPlugin().CreateSerializer();
            var s = (SerialPortOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (s.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(s.TimeoutMs));
            int Remaining()
            {
                epoch.ThrowIfExpired();
                return SerialPortHelper.RemainingBudget(startedAt, s.TimeoutMs);
            }
            Remaining();
            if (s.BaudRate <= 0 || s.DataBits is < 5 or > 8 || s.StopBits is < 1 or > 3 || s.Parity is < 0 or > 4)
                throw new ArgumentException("串口通信参数无效：波特率必须为正数，数据位 5~8，停止位 1/2/3，校验位 0~4");
            if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1 || s.WriteTimeoutMs == 0 || s.WriteTimeoutMs < -1)
                throw new ArgumentException("串口读写期限必须为正数或 -1（无限等待）");

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
            cancellationToken.ThrowIfCancellationRequested();
            var created = _create(portName, s, ex => context.Log(LogLevel.Error, $"串口 {portName} 收尾未确认", ex.ToString()));
            unpublished = created.Owner;
            Remaining();
            await SerialPortLifecycle.OpenRegisteredAsync(context, portName, unpublished,
                () => { Remaining(); created.Open(); Remaining(); }, cancellationToken, Remaining(), () => { Remaining(); });
            Remaining();
            SerialResultPublisher.EnsureActive(context, key, unpublished, cancellationToken);

            context.Log($"串口 {portName} 已打开 (波特率: {s.BaudRate})");
            Remaining();
            SerialResultPublisher.EnsureActive(context, key, unpublished, cancellationToken);
            Remaining();
            unpublished = null;

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
                    Error = ErrorInfo.FromException(ex, $"串口打开失败（含设置解析）: {ex.Message}")
                }
            };
        }
        finally { unpublished?.Dispose(); }
    }

    private static (SerialConnectionResource Owner, Action Open) CreateNative(string name, SerialPortOpenSetting setting, Action<Exception> report)
    {
        var port = new SysSerialPort
        {
            PortName = name, BaudRate = setting.BaudRate, DataBits = setting.DataBits,
            StopBits = (SysStopBits)setting.StopBits, Parity = (SysParity)setting.Parity,
            ReadTimeout = setting.ReadTimeoutMs, WriteTimeout = setting.WriteTimeoutMs
        };
        return (new SerialConnectionResource(port, report), port.Open);
    }
}
