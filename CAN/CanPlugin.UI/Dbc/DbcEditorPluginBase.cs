using System.Globalization;
using System.Windows;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Dbc;

public abstract class DbcEditorPluginBase(IStepPlugin plugin) : IStepEditorPlugin
{
    public string StepTypeId => plugin.StepTypeId;
    public string IconPath => plugin.IconPath ?? "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DbcEditorView(plugin) { SequenceFile = sequenceFile };
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        view.RefreshFromStep(step); return view;
    }
    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        try
        {
            var serializer = plugin.CreateSerializer();
            var setting = (DbcSourceSetting)serializer.Deserialize(context.Setting,
                context.CurrentStep?.StepSetting.SettingVersion ?? serializer.SettingVersion);
            if (setting.TimeoutMs <= 0) errors.Add(StepSettingError.Error("DBC_TIMEOUT", "等待期限必须大于 0 ms"));
            bool hasSource = !string.IsNullOrWhiteSpace(setting.DbcFilePath) || !string.IsNullOrWhiteSpace(setting.Document?.Text);
            if (!hasSource)
                errors.Add(StepSettingError.Error("DBC_DOCUMENT", "请填写 DbcFilePath 绝对文件路径，或选择 DBC 文件并导入内嵌快照"));
            DbcDatabase? database = null;
            if (hasSource && setting.TimeoutMs > 0)
            {
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stop.CancelAfter(setting.TimeoutMs);
                var token = stop.Token;
                var parsing = Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    var document = await DbcDocumentIO.ResolveAsync(setting, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return (await DbcSharedCatalog.LoadAsync(document, token).ConfigureAwait(false)).Database;
                }, CancellationToken.None);
                try { database = await parsing.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { errors.Add(StepSettingError.Error("DBC_PARSE_TIMEOUT", $"DBC 文件读取/解析超过配置等待期限 {setting.TimeoutMs} ms")); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                { errors.Add(StepSettingError.Error("DBC_SOURCE", $"DBC 来源加载失败：{ex.Message}")); }
                finally { _ = parsing.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
            }
            if (database != null && !string.IsNullOrWhiteSpace(setting.CatalogQuery))
                errors.Add(DbcCatalogQuery.Describe(database, setting.CatalogQuery, ct));
            if (database is { Issues.Count: > 0 })
            {
                foreach (var issue in database.Issues)
                    errors.Add(StepSettingError.Warning("DBC_PARTIAL", $"已隔离不可用的报文定义：{issue.DisplayText}"));
                if (database.Messages.Count == 0)
                    errors.Add(StepSettingError.Error("DBC_NO_USABLE_MESSAGES", "DBC 没有通过校验的可用报文，请修复列出的定义问题"));
            }
            if (setting is DbcCanSetting can)
            {
                CheckExpression(can.ConnectionName, "连接名称", "DBC_CONNECTION", context, errors);
                CheckMessages(can, database, context, errors);
                if (context.CurrentStep is { } currentStep)
                    CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, currentStep, can.ConnectionName, errors);
            }
            if (setting is DbcCyclicWriteSetting cyclic)
            {
                CheckExpression(cyclic.TaskName, "周期任务名称", "DBC_TASK", context, errors);
            }
            if (setting is DbcReadBackgroundSetting background)
            {
                CheckExpression(background.TaskName, "总线监听任务名称", "DBC_BACKGROUND_TASK", context, errors);
                if (background.TaskName?.Trim() == "\"\"") errors.Add(StepSettingError.Error("DBC_BACKGROUND_TASK", "总线监听任务名称不能是空字符串"));
                if (background.PublishIntervalMs <= 0 || background.MaxAgeMs <= 0)
                    errors.Add(StepSettingError.Error("DBC_BACKGROUND_INTERVAL", "发布间隔和新鲜度期限必须大于 0 ms"));
            }
            if (setting is DbcCaptureSetting capture)
            {
                if (capture.DurationMs <= 0 || capture.MaxFrames is < 1 or > 100000)
                    errors.Add(StepSettingError.Error("DBC_CAPTURE_LIMIT", "采集时间须大于 0 ms，最大帧数须为 1～100000"));
            }
            if (setting is DbcReadSetting read)
            {
                bool hasMapping = read is not DbcCaptureSetting { AllDbcMessages: true } && (read.UseMessageList
                    ? read.Messages?.Where(m => m?.Enabled == true).Any(m => m.SignalOutputs?.Any(o => o?.Selected == true && !string.IsNullOrWhiteSpace(o.TargetVariable)) == true) == true
                    : read.SignalOutputs?.Any(o => o?.Selected == true && !string.IsNullOrWhiteSpace(o.TargetVariable)) == true);
                CheckResult(read.ResultVariable, !hasMapping, context, errors);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("DBC_SETTINGS", $"DBC 设置无效：{ex.Message}")); }
        return errors;
    }
    private static void CheckMessages(DbcCanSetting setting, DbcDatabase? database, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        var selected = new List<(DbcMessageItem Item, string Label)>();
        if (setting is DbcCaptureSetting { AllDbcMessages: true })
        {
            if (setting.UseMessageList)
            { errors.Add(StepSettingError.Error("DBC_SELECTION_MODE", "采集全部 DBC 与多报文列表不能同时启用")); return; }
            var capture = (DbcCaptureSetting)setting;
            if (capture.SignalOutputs?.Count > 0 ||
                capture.Messages?.Where(m => m?.Enabled == true).Any(m => m.SignalOutputs?.Count > 0) == true)
                errors.Add(StepSettingError.Error("DBC_OUTPUT_ALL_MODE", "采集全部 DBC 仅支持完整 JSON；信号筛选或映射请改用指定 ID 模式，或清空已有信号输出配置"));
            if (database != null)
                selected.AddRange(database.Messages.Select(m => (new DbcMessageItem { MessageId = m.Id,
                    FrameType = m.Extended ? CanFrameType.Extended : CanFrameType.Standard,
                    FrameFormat = setting.FrameFormat, Protection = setting.Protection }, m.Name)));
        }
        else if (setting.UseMessageList)
        {
            if (setting.Messages == null || setting.Messages.Count is < 1 or > 128)
            { errors.Add(StepSettingError.Error("DBC_MESSAGE_COUNT", "DBC 多报文列表需要 1～128 行")); return; }
            for (int i = 0; i < setting.Messages.Count; i++)
            {
                var item = setting.Messages[i];
                if (item == null) errors.Add(StepSettingError.Error("DBC_MESSAGE", $"DBC 列表第 {i + 1} 行不能为空"));
                else if (item.Enabled) selected.Add((item, $"第 {i + 1} 行 0x{item.MessageId:X}"));
            }
            if (selected.Count == 0) errors.Add(StepSettingError.Error("DBC_MESSAGE_COUNT", "DBC 多报文列表至少启用一个 ID"));
        }
        else selected.Add((new DbcMessageItem { MessageId = setting.MessageId, FrameType = setting.FrameType,
            FrameFormat = setting.FrameFormat, Protection = setting.Protection,
            Signals = setting is DbcWriteSetting write ? write.Signals : [],
            SignalOutputs = setting is DbcReadSetting read ? read.SignalOutputs : [],
            UseDbcCycleTime = setting is DbcCyclicWriteSetting { UseDbcCycleTime: true },
            CycleTimeMs = (setting as DbcCyclicWriteSetting)?.CycleTimeMs ?? 100 }, $"0x{setting.MessageId:X}"));

        var identities = new HashSet<(uint, CanFrameType)>();
        var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (setting is DbcReadSetting result && !string.IsNullOrWhiteSpace(result.ResultVariable)) outputPaths.Add(result.ResultVariable);
        foreach (var (item, label) in selected)
        {
            if (!Enum.IsDefined(item.FrameType) || !Enum.IsDefined(item.FrameFormat))
            { errors.Add(StepSettingError.Error("DBC_FRAME_FORMAT", $"{label} 的 CAN 帧类型或格式无效")); continue; }
            if (!identities.Add((item.MessageId, item.FrameType)))
                errors.Add(StepSettingError.Error("DBC_MESSAGE_DUPLICATE", $"{label} 的 ID/标准扩展帧类型重复"));
            PayloadProtectionValidator.CheckTableExpression(item.Protection, context, errors);
            if (database == null) continue;
            DbcMessage message;
            try { message = database.Find(item.MessageId, item.FrameType == CanFrameType.Extended); }
            catch (KeyNotFoundException ex) { errors.Add(StepSettingError.Error("DBC_MESSAGE", $"{label}：{ex.Message}")); continue; }
            if (setting is DbcCyclicWriteSetting)
            {
                try { _ = DbcCycleTiming.Resolve(message, item.UseDbcCycleTime, item.CycleTimeMs); }
                catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DBC_PERIOD", $"{label}：{ex.Message}")); }
            }
            try { _ = DbcPayload.ResolveFd(message, item.FrameFormat); }
            catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DBC_FRAME_FORMAT", $"{label}：{ex.Message}")); }
            var layoutProtection = CanPayloadProtectionExpressions.ForLayoutValidation(item.Protection);
            try
            {
                if (setting is DbcReadSetting) DbcPayload.ValidateReadProtection(layoutProtection, message.Length);
                else CanPayloadProtection.Validate(layoutProtection, message.Length);
            }
            catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DBC_PROTECTION", $"{label} 的 RC/CRC 配置无效：{ex.Message}")); }
            if (setting is DbcWriteSetting) CheckSignals(item, message, label, context, errors);
            if (setting is DbcReadSetting) CheckOutputs(item, message, label, setting is DbcCaptureSetting, outputPaths, context, errors);
        }
    }
    private static void CheckOutputs(DbcMessageItem setting, DbcMessage message, string label, bool capture,
        HashSet<string> outputPaths, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (setting.SignalOutputs == null)
        { errors.Add(StepSettingError.Error("DBC_OUTPUTS", $"{label} 信号输出配置不能为空")); return; }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in setting.SignalOutputs)
        {
            if (output == null || !names.Add(output.Name))
            { errors.Add(StepSettingError.Error("DBC_OUTPUT_DUPLICATE", $"{label} 信号输出配置为空或名称重复")); continue; }
            if (!message.Signals.Any(s => s.Name == output.Name))
                errors.Add(StepSettingError.Error("DBC_OUTPUT_UNKNOWN", $"{label} 当前报文没有输出信号 {output.Name}"));
            if (!output.Selected || string.IsNullOrWhiteSpace(output.TargetVariable)) continue;
            if (!Enum.IsDefined(output.ValueKind))
                errors.Add(StepSettingError.Error("DBC_OUTPUT_KIND", $"{label} 信号 {output.Name} 的物理/原始输出类型无效"));
            if (!outputPaths.Add(output.TargetVariable))
                errors.Add(StepSettingError.Error("DBC_OUTPUT_TARGET_DUPLICATE", $"{label} 信号 {output.Name} 的目标 {output.TargetVariable} 与其他信号或 JSON 目标重复"));
            try { DbcVariableBinding.ValidateTarget(output.TargetVariable, context.ExecutionContext, capture); }
            catch (ArgumentException ex)
            { errors.Add(StepSettingError.Error("DBC_OUTPUT_TARGET", $"{label} 信号 {output.Name}：{ex.Message}")); }
        }
    }
    private static void CheckSignals(DbcMessageItem setting, DbcMessage message, string label, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (setting.Signals == null) { errors.Add(StepSettingError.Error("DBC_SIGNALS", $"{label} 信号配置不能为空")); return; }
        IReadOnlySet<string> automaticSignals;
        try { automaticSignals = DbcPayload.ValidateProtection(message, CanPayloadProtectionExpressions.ForLayoutValidation(setting.Protection)); }
        catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DBC_SIGNAL_PROTECTION_OVERLAP", $"{label}：{ex.Message}")); return; }
        // Only a literal or the DBC default can determine a branch without running
        // user expressions in the editor. A dynamic selector validates every
        // potentially active selected branch conservatively; runtime evaluates one.
        decimal? knownMux = null;
        if (message.MultiplexerSignal is { } selector)
        {
            var selectorInput = setting.Signals.FirstOrDefault(s => s?.Name == selector.Name);
            if (selectorInput == null || !selectorInput.Selected) knownMux = selector.InitialRawValue;
            else if (decimal.TryParse(selectorInput.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var literal))
            {
                try { knownMux = selector.ToRaw(literal, selectorInput.ValueKind == DbcValueKind.Raw); }
                catch (ArgumentException) { /* Report this selector through the shared signal check below. */ }
            }
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in setting.Signals)
        {
            if (input == null || !names.Add(input.Name)) { errors.Add(StepSettingError.Error("DBC_SIGNAL_DUPLICATE", $"{label} 信号配置为空或名称重复")); continue; }
            var signal = message.Signals.FirstOrDefault(s => s.Name == input.Name);
            if (signal == null) { errors.Add(StepSettingError.Error("DBC_SIGNAL_UNKNOWN", $"{label} 当前报文没有信号 {input.Name}")); continue; }
            bool automatic = automaticSignals.Contains(signal.Name);
            if (!input.Selected || automatic || knownMux.HasValue && signal.MultiplexerValue.HasValue && signal.MultiplexerValue != knownMux) continue;
            if (!Enum.IsDefined(input.ValueKind)) errors.Add(StepSettingError.Error("DBC_SIGNAL_KIND", $"{label} 信号 {input.Name} 的物理/原始值类型无效"));
            CheckExpression(input.Value, $"{label} 信号 {input.Name}", "DBC_SIGNAL_EXPRESSION", context, errors);
            if (decimal.TryParse(input.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                try { _ = signal.ToRaw(DbcNumber.ParseExact(input.Value), input.ValueKind == DbcValueKind.Raw); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException or FormatException)
                { errors.Add(StepSettingError.Error("DBC_SIGNAL_VALUE", $"{label} 信号 {input.Name}：{ex.Message}")); }
            }
        }
    }
    private static void CheckExpression(string text, string title, string code, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(text)) errors.Add(StepSettingError.Error(code, $"{title}不能为空"));
        else if (!context.Evaluator.ValidateExpression(text, context.ExecutionContext, out var detail))
            errors.Add(StepSettingError.Error(code, $"{title}表达式无效：{detail}"));
    }
    private static void CheckResult(string path, bool required, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(path) && !required) return;
        try { DbcVariableBinding.ValidateJsonTarget(path, context.ExecutionContext); }
        catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DBC_RESULT", ex.Message)); }
    }
}
