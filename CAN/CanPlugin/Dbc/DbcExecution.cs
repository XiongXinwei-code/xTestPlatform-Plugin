using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Dbc;

internal abstract class DbcExecutorBase<T> : IStepExecutor where T : DbcSourceSetting
{
    protected abstract IStepPlugin Plugin { get; }
    protected abstract Task<ExecutionResult> RunAsync(T setting, DbcExecution run);
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        DbcExecution? run = null;
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (T)Plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            run = new(context, guard, setting.TimeoutMs, cancellationToken);
            return await RunAsync(setting, run).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return ExecutionResults.Aborted(); }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && run?.PreparationExpired == true)
                ex = new TimeoutException($"DBC 操作超过配置期限 {run.TimeoutMs} ms", ex);
            return ExecutionResults.FromException(ex, $"{Plugin.DisplayName} 执行失败：{ex.Message}");
        }
        finally { run?.Dispose(); }
    }
}

internal sealed class DbcExecution : IDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly CancellationTokenSource _stop;
    private readonly IDisposable _owned;
    private readonly long _started = Stopwatch.GetTimestamp();
    private bool _preparation = true;
    internal IExecutionContext Context { get; }
    internal CanExecutionGuard Guard { get; }
    internal int TimeoutMs { get; }
    internal CancellationToken Token => _stop.Token;
    internal bool PreparationExpired => _preparation && Stopwatch.GetElapsedTime(_started).TotalMilliseconds >= TimeoutMs;

    internal DbcExecution(IExecutionContext context, CanExecutionGuard guard, int timeoutMs, CancellationToken caller)
    {
        if (timeoutMs <= 0) throw new ArgumentException("DBC 操作等待期限必须大于 0 ms");
        Context = context; Guard = guard; TimeoutMs = timeoutMs;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _stop.CancelAfter(timeoutMs);
        _owned = ExecutionScopedExecutor.OwnOperation(() => _ = _stop.CancelAsync());
    }

    internal void Check() { Guard.Check(); Token.ThrowIfCancellationRequested(); if (PreparationExpired) throw new TimeoutException($"DBC 操作超过配置期限 {TimeoutMs} ms"); }
    internal int Remaining()
    {
        Check(); return Math.Max(1, TimeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(_started).TotalMilliseconds));
    }
    internal void BeginCapturePeriod()
    {
        Check(); _stop.CancelAfter(Timeout.Infinite); _preparation = false; Token.ThrowIfCancellationRequested();
    }
    internal async Task<(DbcDatabase Database, string Fingerprint)> LoadAsync(DbcSourceSetting setting)
    {
        Check();
        var document = await DbcDocumentIO.ResolveAsync(setting, Token).ConfigureAwait(false);
        Check();
        var result = await DbcCatalogCache.LoadAsync(document, Context, Guard, Token).ConfigureAwait(false);
        Check();
        if (result.Database.Issues.Count > 0)
        {
            string details = string.Join(Environment.NewLine, result.Database.Issues.Select(i => i.DisplayText));
            Guard.Log(LogLevel.Warn, $"DBC 部分加载：可用 {result.Database.Messages.Count} 个报文，发现 {result.Database.Issues.Count} 项问题；有问题的 ID 已隔离，详见日志详情", details);
            if (result.Database.Messages.Count == 0)
                throw new InvalidOperationException("DBC 没有通过校验的可用报文；" + result.Database.Issues[0].DisplayText);
        }
        Check(); return result;
    }
    internal async Task<string> TextAsync(string expression)
    {
        Check(); string result = await Evaluator.EvalStringAsync(expression, Context).WaitAsync(Token).ConfigureAwait(false); Check(); return result;
    }
    internal async Task<CanConnectionResource> ConnectionAsync(DbcCanSetting setting)
    {
        string name = await TextAsync(setting.ConnectionName).ConfigureAwait(false);
        string key = CanHelper.GetAdapterKey(name);
        if (!Context.Resources.TryGet<CanConnectionResource>(key, out var owner)) throw new InvalidOperationException($"CAN 连接 {name} 未找到，请先执行 CAN_Open");
        Guard.Watch(key, owner); Check(); return owner;
    }
    internal static DbcMessage Select(DbcDatabase database, DbcCanSetting setting)
    {
        if (!Enum.IsDefined(setting.FrameType)) throw new ArgumentException("标准/扩展帧选择无效");
        var message = database.Find(setting.MessageId, setting.FrameType == CanFrameType.Extended);
        _ = DbcPayload.ResolveFd(message, setting.FrameFormat);
        return message;
    }
    internal async Task<DbcSelectedMessage[]> SelectMessagesAsync(DbcDatabase database, DbcCanSetting setting)
    {
        var items = new List<(DbcMessageItem Item, int Row)>();
        if (setting is DbcCaptureSetting { AllDbcMessages: true } all)
        {
            if (setting.UseMessageList) throw new ArgumentException("采集全部 DBC 与多报文列表不能同时启用");
            if ((all.SignalOutputs ?? []).Count > 0 ||
                (all.Messages ?? []).Where(m => m?.Enabled == true).Any(m => (m.SignalOutputs ?? []).Count > 0))
                throw new ArgumentException("采集全部 DBC 仅支持完整 JSON；逐信号筛选或映射请选择多报文列表及具体 ID");
            items.AddRange(database.Messages.Select(m => (new DbcMessageItem { MessageId = m.Id,
                FrameType = m.Extended ? CanFrameType.Extended : CanFrameType.Standard,
                FrameFormat = setting.FrameFormat, Protection = setting.Protection }, 0)));
        }
        else if (setting.UseMessageList)
        {
            if (setting.Messages == null || setting.Messages.Count is < 1 or > 128)
                throw new ArgumentException("DBC 多报文列表需要 1～128 行");
            for (int i = 0; i < setting.Messages.Count; i++)
            {
                var item = setting.Messages[i] ?? throw new ArgumentException($"DBC 列表第 {i + 1} 行为空");
                if (item.Enabled) items.Add((item, i + 1));
            }
            if (items.Count == 0) throw new ArgumentException("DBC 多报文列表没有启用的 ID");
        }
        else items.Add((new DbcMessageItem { MessageId = setting.MessageId, FrameType = setting.FrameType,
            FrameFormat = setting.FrameFormat, Protection = setting.Protection,
            Signals = setting is DbcWriteSetting write ? write.Signals : [],
            SignalOutputs = setting is DbcReadSetting read ? read.SignalOutputs : [],
            UseDbcCycleTime = setting is DbcCyclicWriteSetting cyclic && cyclic.UseDbcCycleTime,
            CycleTimeMs = setting is DbcCyclicWriteSetting cycle ? cycle.CycleTimeMs : 100 }, 0));
        var result = new List<DbcSelectedMessage>();
        var identities = new HashSet<(uint, CanFrameType)>();
        foreach (var (item, row) in items)
        {
            Check();
            try
            {
                if (!Enum.IsDefined(item.FrameType) || !identities.Add((item.MessageId, item.FrameType)))
                    throw new ArgumentException("报文帧类型无效或同一 ID/帧类型重复");
                var message = database.Find(item.MessageId, item.FrameType == CanFrameType.Extended);
                _ = DbcPayload.ResolveFd(message, item.FrameFormat);
                var protection = await CanPayloadProtectionExpressions.ResolveAsync(item.Protection, Context, Token, Check, Guard.Log).ConfigureAwait(false);
                if (setting is DbcWriteSetting) _ = DbcPayload.ValidateProtection(message, protection);
                else DbcPayload.ValidateReadProtection(protection, message.Length);
                var inputs = (item.Signals ?? []).Select(s => s == null ? throw new ArgumentException("信号行不能为空") :
                    new DbcSignalInput { Name = s.Name, Value = s.Value, ValueKind = s.ValueKind, Selected = s.Selected }).ToArray();
                var outputs = (item.SignalOutputs ?? []).Select(s => s == null ? throw new ArgumentException("输出信号行不能为空") :
                    new DbcSignalOutput { Name = s.Name, TargetVariable = s.TargetVariable, ValueKind = s.ValueKind, Selected = s.Selected }).ToArray();
                int period = setting is DbcCyclicWriteSetting ? DbcCycleTiming.Resolve(message, item.UseDbcCycleTime, item.CycleTimeMs) : 0;
                result.Add(new(message, item.FrameFormat, protection, inputs, row, outputs, period));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new InvalidOperationException($"DBC {(row == 0 ? "单报文/全部采集" : $"列表第 {row} 行")} 0x{item.MessageId:X} 配置失败：{ex.Message}", ex); }
        }
        return result.ToArray();
    }
    internal void ValidateResult(string path, bool optional = false)
    {
        if (optional && string.IsNullOrWhiteSpace(path)) return;
        DbcVariableBinding.ValidateJsonTarget(path, Context);
    }
    internal async Task PublishAsync(string path, object result)
    {
        Check(); string json = await DbcJson.SerializeAsync(result, Token).ConfigureAwait(false); Check();
        if (!string.IsNullOrWhiteSpace(path)) Guard.SetVariable(path, json);
    }
    internal async Task PrepareReceiveAsync(CanConnectionResource owner)
    {
        var task = Task.Run(() => { Check(); owner.EnsureSharedReceive(Remaining(), Token); Check(); }, CancellationToken.None);
        try { await Guard.RunAsync(() => task.WaitAsync(Token)).ConfigureAwait(false); Check(); }
        catch { _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); throw; }
    }

    internal static async Task<byte[]> EncodeAsync(DbcMessage message, DbcSignalInput[] inputs, CanPayloadProtectionSetting protection,
        IExecutionContext context, CancellationToken ct, Action check, Action<Task>? track = null)
    {
        check(); ct.ThrowIfCancellationRequested();
        var automatic = DbcPayload.ValidateProtection(message, protection);
        var definitions = message.Signals.ToDictionary(s => s.Name, StringComparer.Ordinal);
        var byName = new Dictionary<string, DbcSignalInput>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            if (input == null || !definitions.ContainsKey(input.Name) || !byName.TryAdd(input.Name, input))
                throw new ArgumentException($"DBC 信号配置包含未知或重复名称：{input?.Name}");
        }
        var values = new Dictionary<string, DbcSignalValue>(StringComparer.Ordinal);
        async Task<DbcSignalValue> Evaluate(DbcSignal signal)
        {
            if (automatic.Contains(signal.Name)) return new(0, true);
            if (!byName.TryGetValue(signal.Name, out var input) || !input.Selected) return new(signal.InitialRawValue, true);
            if (!Enum.IsDefined(input.ValueKind)) throw new ArgumentException($"信号 {input.Name} 的物理值/原始值选择无效");
            check(); ct.ThrowIfCancellationRequested();
            var actual = Task.Run(async () =>
            {
                check(); ct.ThrowIfCancellationRequested();
                var result = await Evaluator.EvaluateAsync<object>(input.Value, context).ConfigureAwait(false);
                check(); ct.ThrowIfCancellationRequested(); return result;
            }, CancellationToken.None);
            track?.Invoke(actual);
            _ = actual.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            object? value = await actual.WaitAsync(ct).ConfigureAwait(false);
            check(); ct.ThrowIfCancellationRequested();
            if (value is double d && (!double.IsFinite(d) || Math.Abs(d) >= 9007199254740992d)
                || value is float f && (!float.IsFinite(f) || Math.Abs(f) >= 16777216f))
                throw new ArgumentException($"信号 {signal.Name} 的浮点表达式可能丢失整数精度，请使用 decimal 或十进制/十六进制字符串表达式");
            string text = value is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
            decimal parsed;
            if (input.ValueKind == DbcValueKind.Raw && text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                parsed = ulong.Parse(text.Trim()[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            else parsed = DbcNumber.ParseExact(text);
            return new(parsed, input.ValueKind == DbcValueKind.Raw);
        }
        decimal? mux = null;
        if (message.MultiplexerSignal is { } selector)
        {
            var value = await Evaluate(selector).ConfigureAwait(false); values[selector.Name] = value;
            mux = selector.ToRaw(value.Value, value.IsRaw);
        }
        foreach (var signal in message.GetActiveSignals(mux))
            if (!values.ContainsKey(signal.Name)) values[signal.Name] = await Evaluate(signal).ConfigureAwait(false);
        check(); ct.ThrowIfCancellationRequested();
        return message.Encode(values);
    }

    internal static CanMessage Frame(DbcMessage message, DbcFrameFormat format, byte[] bytes) => new()
    { Id = message.Id, FrameType = message.Extended ? CanFrameType.Extended : CanFrameType.Standard, IsFd = DbcPayload.ResolveFd(message, format), Data = bytes };

    public void Dispose() { _owned.Dispose(); _stop.Dispose(); }
}

internal sealed record DbcSelectedMessage(DbcMessage Message, DbcFrameFormat Format,
    CanPayloadProtectionSetting Protection, DbcSignalInput[] Inputs, int Row, DbcSignalOutput[] Outputs, int CycleTimeMs = 0)
{
    internal IReadOnlyList<DbcDecodedSignal> FilterSignals(IReadOnlyList<DbcDecodedSignal> signals)
    {
        var excluded = Outputs.Where(output => !output.Selected).Select(output => output.Name).ToHashSet(StringComparer.Ordinal);
        return excluded.Count == 0 ? signals : signals.Where(signal => !excluded.Contains(signal.Name)).ToArray();
    }
    internal (uint Id, CanFrameType Type, bool Fd) Identity => (Message.Id,
        Message.Extended ? CanFrameType.Extended : CanFrameType.Standard, DbcPayload.ResolveFd(Message, Format));
}
