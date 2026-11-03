using System.Diagnostics;
using System.Text.Json;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

internal sealed class DaqExpansionExecutor(IStepPlugin plugin, IDaqExpansionBackend? backend = null,
    Func<IEnumerable<string>, DaqTaskResource>? createOwner = null) : IStepExecutor
{
    private readonly IDaqExpansionBackend _backend = backend ?? new DaqExpansionBackend();
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        DaqTaskResource? created = null;
        try
        {
            var guard = new DaqExecutionGuard(context, cancellationToken);
            long started = Stopwatch.GetTimestamp();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("DAQ 缺少步骤上下文");
            var s = (DaqNamedSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            DaqExpansionValidation.Validate(s);
            int Remaining() { guard.Check(); return DaqTaskFactory.RemainingBudget(started, s.TimeoutMs); }
            async Task<T> Evaluate<T>(string expression) where T : class => await ExpressionEvaluatorFactory.Default.EvaluateAsync<T>(expression, context)
                .WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false)
                ?? throw new ArgumentException("表达式结果不能为 null");
            var name = await Evaluate<string>(s.TaskName).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("任务名求值结果不能为空");
            guard.Check();
            if (s is NiDaqCounterInputConfigSetting or NiDaqCounterOutputConfigSetting or DaqDigitalConfigSetting)
            {
                var expression = s switch { NiDaqCounterInputConfigSetting input => input.CounterChannel, NiDaqCounterOutputConfigSetting output => output.CounterChannel, DaqDigitalConfigSetting digital => digital.Channels, _ => throw new InvalidOperationException() };
                string channelText = await Evaluate<string>(expression).ConfigureAwait(false);
                string[] physical = s is DaqDigitalConfigSetting ? DaqExpansionValidation.DigitalChannels(channelText) : [DaqExpansionValidation.Counter(channelText)];
                guard.Check();
                created = await DaqTaskFactory.CreateAndRegisterAsync(context, name, owner => _backend.Configure(owner, s, physical), cancellationToken,
                    ownedResource: createOwner?.Invoke(physical), physicalChannels: physical, timeoutMs: Remaining(), epoch: guard.Epoch).ConfigureAwait(false);
                guard.EnsureCurrent(name, created); Remaining();
                return Success($"任务 '{name}' 已配置；尚未启动，使用 TaskStart 启动、TaskStop 释放");
            }
            var resource = NiDaqTaskRegistry.Get(context, name, guard.Check);
            guard.EnsureCurrent(name, resource);
            switch (s)
            {
                case NiDaqCounterReadSetting read:
                    resource.RequireKind(DaqTaskKind.CounterEdges, DaqTaskKind.CounterFrequency);
                    var scalar = await resource.RunAsync(() => { resource.RequireStarted(); return _backend.ReadCounter(resource); }, Remaining(), "计数器读取", cancellationToken).ConfigureAwait(false);
                    if (resource.Kind == DaqTaskKind.CounterEdges && scalar is not uint || resource.Kind == DaqTaskKind.CounterFrequency && (scalar is not double frequency || !double.IsFinite(frequency))) throw new IOException("计数器返回类型或数值无效");
                    guard.EnsureCurrent(name, resource); Remaining();
                    DaqResultPublisher.Publish(context, read.ResultVariable, scalar, cancellationToken, resource, name);
                    guard.EnsureCurrent(name, resource); Remaining(); return Success(Convert.ToString(scalar, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                case NiDaqDigitalReadSetting read:
                    resource.RequireKind(DaqTaskKind.DigitalInput);
                    DaqExpansionValidation.CheckShape(resource.DigitalChannels.Length, read.SamplesPerChannel, resource.DigitalChannels.Length);
                    if (!resource.DigitalBuffered && read.SamplesPerChannel != 1) throw new ArgumentException("按需数字量每次只能读取一个样本，请配置采样时钟后读取多样本");
                    var json = await resource.RunAsync(() =>
                    {
                        resource.RequireStarted(); var samples = _backend.ReadDigital(resource, read.SamplesPerChannel);
                        resource.EnsureOperationActive();
                        if (samples.GetLength(0) != resource.DigitalChannels.Length || samples.GetLength(1) != read.SamplesPerChannel) throw new IOException("数字量实际返回通道数/样本数不匹配，拒绝发布不完整结果");
                        var rows = new uint[samples.GetLength(0)][];
                        for (int c = 0; c < rows.Length; c++)
                        {
                            resource.EnsureOperationActive(); rows[c] = new uint[samples.GetLength(1)];
                            for (int n = 0; n < rows[c].Length; n++) { if ((n & 1023) == 0) resource.EnsureOperationActive(); rows[c][n] = samples[c, n]; }
                        }
                        resource.EnsureOperationActive();
                        string privateJson = JsonSerializer.Serialize(new { channels = resource.DigitalChannels, samplesPerChannel = samples.GetLength(1), data = rows });
                        resource.EnsureOperationActive(); return privateJson;
                    }, Remaining(), "数字量读取及结果转换", cancellationToken).ConfigureAwait(false);
                    guard.EnsureCurrent(name, resource); Remaining();
                    DaqResultPublisher.Publish(context, read.ResultVariable, json, cancellationToken, resource, name);
                    guard.EnsureCurrent(name, resource); Remaining(); return Success($"{resource.DigitalChannels.Length} 通道 × {read.SamplesPerChannel} 样本");
                case NiDaqDigitalWriteSetting write:
                    resource.RequireKind(DaqTaskKind.DigitalOutput);
                    var evaluated = await Evaluate<object>(write.Values).ConfigureAwait(false);
                    guard.EnsureCurrent(name, resource);
                    int written = await resource.RunAsync(() =>
                    {
                        var values = DaqExpansionValidation.DigitalValues(evaluated, resource.DigitalChannels.Length, resource.EnsureOperationActive);
                        if (!resource.DigitalBuffered) { resource.RequireStarted(); if (values.GetLength(1) != 1) throw new ArgumentException("按需数字量每通道只写一个样本"); }
                        for (int c = 0; c < values.GetLength(0); c++) for (int n = 0; n < values.GetLength(1); n++)
                        {
                            resource.EnsureOperationActive();
                            if ((values[c, n] & ~resource.DigitalMasks[c]) != 0) throw new ArgumentException($"第 {c + 1} 个数字通道输出包含未选择的物理端口位，拒绝截断；允许掩码=0x{resource.DigitalMasks[c]:X8}");
                        }
                        resource.EnsureOperationActive(); _backend.WriteDigital(resource, values); return values.GetLength(1);
                    }, Remaining(), "数字量写入", cancellationToken).ConfigureAwait(false);
                    guard.EnsureCurrent(name, resource); Remaining(); return Success($"SDK 已接受 {resource.DigitalChannels.Length} 通道 × {written} 样本；不等于物理输出完成");
                default: throw new ArgumentException("不支持的 DAQ 步骤设置");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        { created?.Dispose(); return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        { created?.Dispose(); return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, "DAQ 操作失败：" + ex.Message) } }; }
    }
    private static ExecutionResult Success(string value) => new() { StepResult = new() { Status = TestStatus.Completed, Value = value } };
}
