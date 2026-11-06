using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Data;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Dbc;

public sealed record DbcMessageChoice(DbcMessage Message)
{
    private static readonly ConditionalWeakTable<DbcMessage, string> Labels = new();
    public string Display => Labels.GetValue(Message, static message =>
        $"0x{message.Id:X} [{(message.Extended ? "扩展" : "标准")}] {message.Name} ({message.Length} 字节{(message.IsFd ? ", FD" : "")})");
}

public sealed class DbcConfiguredMessageRow(DbcMessageItem item, Func<DbcMessageItem, string> display,
    Func<DbcMessageItem, string> timing, Func<DbcMessageItem, int?> dbcPeriod) : INotifyPropertyChanged
{
    public DbcMessageItem Item { get; } = item;
    public bool Enabled { get => Item.Enabled; set { if (Item.Enabled != value) { Item.Enabled = value; Refresh(); } } }
    public string Display => display(Item);
    public string ProtectionSummary => DbcEditorViewModel.DescribeProtection(Item.Protection);
    public string TimingSummary => timing(Item);
    public bool UseDbcCycleTime
    {
        get => Item.UseDbcCycleTime;
        set
        {
            if (Item.UseDbcCycleTime == value) return;
            if (!value && dbcPeriod(Item) is { } period) Item.CycleTimeMs = period;
            Item.UseDbcCycleTime = value;
        }
    }
    public int CycleTimeMs { get => Item.CycleTimeMs; set => Item.CycleTimeMs = value; }
    internal void Refresh() => PropertyChanged?.Invoke(this, new(string.Empty));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DbcSignalRow : INotifyPropertyChanged
{
    private readonly DbcSignalInput? _input;
    private readonly DbcSignalOutput? _output;
    private readonly CanPayloadProtectionSetting? _protection;
    private readonly Func<bool>? _canModify;
    private Action? _registerInput;
    private Action? _registerOutput;
    public DbcSignalRow(DbcSignal signal, DbcSignalInput? input, CanPayloadProtectionSetting? protection,
        Action? registerInput = null, DbcSignalOutput? output = null, Action? registerOutput = null,
        DbcMessage? message = null, bool messageEnabled = true, Func<bool>? canModify = null,
        string? messageDisplay = null, string? messageGroup = null)
    {
        Signal = signal; _input = input; _output = output; _protection = protection;
        _registerInput = registerInput; _registerOutput = registerOutput; _canModify = canModify;
        MessageId = message?.Id ?? 0; IsExtended = message?.Extended == true; MessageName = message?.Name ?? "";
        MessageDisplay = messageDisplay ?? (message == null ? "" : new DbcMessageChoice(message).Display);
        MessageGroup = messageGroup ?? GroupLabel(MessageDisplay, messageEnabled);
    }
    public DbcSignal Signal { get; }
    public uint MessageId { get; }
    public bool IsExtended { get; }
    public string MessageName { get; }
    public string MessageDisplay { get; }
    public string MessageGroup { get; }
    internal static string GroupLabel(string display, bool enabled) => $"{display} · {(enabled ? "已启用" : "已禁用（仅编辑配置，运行不执行）")}";
    private bool CanModify => _canModify?.Invoke() ?? true;
    public string Name => Signal.Name;
    public string Unit => Signal.Unit;
    public string Layout => $"{Signal.StartBit}|{Signal.BitLength} {(Signal.IsLittleEndian ? "Intel" : "Motorola")} {(Signal.IsSigned ? "有符号" : "无符号")}";
    public string Scaling => $"× {Signal.Factor.ToString(CultureInfo.InvariantCulture)} + {Signal.Offset.ToString(CultureInfo.InvariantCulture)}";
    public string Range => $"[{Signal.Minimum.ToString(CultureInfo.InvariantCulture)}, {Signal.Maximum.ToString(CultureInfo.InvariantCulture)}]";
    public string Multiplex => Signal.IsMultiplexer ? "复用选择器 M" : Signal.MultiplexerValue is { } value ? $"复用分支 m{value.ToString(CultureInfo.InvariantCulture)}" : "";
    public string Meaning => string.Join("; ", Signal.ValueDescriptions.Take(20).Select(pair => $"{pair.Key.ToString(CultureInfo.InvariantCulture)}={pair.Value}"));
    public string Comment => Signal.Comment;
    public string DefaultValue => Signal.HasInitialValue ? $"DBC 原始初值 {Signal.InitialRawValue.ToString(CultureInfo.InvariantCulture)}" : "未声明初值：原始值 0";
    public bool Selected { get => _input?.Selected == true; set { if (!CanModify || _input == null || _input.Selected == value) return; RegisterInput(); _input.Selected = value; Changed(); Changed(nameof(IsEditable)); } }
    public string Value { get => _input?.Value ?? ""; set { if (!CanModify || _input == null || _input.Value == value) return; RegisterInput(); _input.Value = value; Changed(); } }
    public int ValueKind { get => (int)(_input?.ValueKind ?? DbcValueKind.Physical); set { if (!CanModify || _input == null || (int)_input.ValueKind == value) return; RegisterInput(); _input.ValueKind = (DbcValueKind)value; Changed(); } }
    private void RegisterInput() { _registerInput?.Invoke(); _registerInput = null; }
    public string OutputVariable { get => _output?.TargetVariable ?? ""; set { if (!CanModify || _output == null || _output.TargetVariable == value) return; RegisterOutput(); _output.TargetVariable = value; Changed(); } }
    public int OutputValueKind { get => (int)(_output?.ValueKind ?? DbcValueKind.Physical); set { if (!CanModify || _output == null || (int)_output.ValueKind == value) return; RegisterOutput(); _output.ValueKind = (DbcValueKind)value; Changed(); } }
    public bool OutputSelected
    {
        get => _output?.Selected == true;
        set
        {
            if (!CanModify || _output == null || _output.Selected == value) return;
            RegisterOutput(); _output.Selected = value; Changed(); Changed(nameof(CanBindOutput));
        }
    }
    public bool CanSelectOutput => CanModify && _output != null;
    public bool CanBindOutput => CanModify && _output?.Selected == true;
    private void RegisterOutput() { _registerOutput?.Invoke(); _registerOutput = null; }
    public bool CanSelect => CanModify && _input != null && !IsAutomatic;
    public bool IsEditable => CanSelect && Selected;
    public bool IsAutomatic => _protection != null && Signal.BitPositions().All(bit => CanPayloadProtection.ControlsBit(_protection, bit));
    public string ProtectionNote => IsAutomatic ? "由 RC/CRC/Data ID 填写" : "";
    internal void RefreshProtection() => Changed(string.Empty);
    internal bool UsesProtection(object? protection) => ReferenceEquals(_protection, protection);
    internal bool UsesSignal(object? source) => ReferenceEquals(_input, source) || ReferenceEquals(_output, source);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

/// <summary>所有解析均在后台运行；代次与步骤身份阻止旧加载结果修改新编辑页。</summary>
public sealed class DbcEditorViewModel : INotifyPropertyChanged
{
    private readonly IStepPlugin _plugin;
    private readonly IStepSettingSerializer _serializer;
    private readonly Func<string, string, CancellationToken, Task<DbcDocumentSetting>> _readDocument;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get; set; }
    // Keep only a digest, not another copy of every embedded multi-MiB document.
    private sealed record SettingStamp(int Version, int Length, byte[] Digest)
    {
        public static SettingStamp Capture(int version, byte[]? bytes) => new(version, bytes?.Length ?? 0, SHA256.HashData(bytes.AsSpan()));
        public bool Matches(Step step)
        {
            var current = step.StepSetting.Setting.AsSpan();
            if (step.StepSetting.SettingVersion != Version || current.Length != Length) return false;
            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(current, hash);
            return hash.SequenceEqual(Digest);
        }
    }
    private SettingStamp? _loadedStamp;
    private SettingStamp? _ownPublication;
    private Step? _conflictStep;
    private int _attachFlushDepth;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Func<Action, Task>? _postToEditor;
    private Step? _step;
    private DbcSourceSetting? _setting;
    private DbcDatabase? _database;
    private Dictionary<(uint Id, bool Extended), DbcMessageChoice> _choices = [];
    private CancellationTokenSource? _loadStop;
    private long _generation;
    private bool _suppress;
    private readonly HashSet<CanPayloadProtectionSetting> _observedProtections = [];
    private readonly HashSet<ObservableCollection<DbcSignalInput>> _observedInputs = [];
    private readonly HashSet<DbcSignalInput> _observedRows = [];
    private readonly HashSet<ObservableCollection<DbcSignalOutput>> _observedOutputs = [];
    private readonly HashSet<DbcSignalOutput> _observedOutputRows = [];
    private ObservableCollection<DbcMessageItem>? _observedMessages;
    private readonly HashSet<DbcMessageItem> _observedMessageItems = [];
    private DbcConfiguredMessageRow? _selectedConfiguredMessage;
    private (uint Id, CanFrameType Type)? _suspendedSelection;
    private DbcMessageChoice? _messageToAdd;
    private DbcMessageChoice? _selectedMessage;
    private string _loadError = "";
    private string _previewDocumentName = "";
    private string _previewEncoding = "";
    private bool _isLoading;
    private bool _active;
    private bool _batchMutation;
    private bool _rebuildingMessages;
    private bool _updatingActiveMessage;
    private bool _changingSignalCollection;
    private long _editIdentity;
    private long _signalRowsGeneration;
    private int _signalMessageCount;
    private const int PageSize = 50;
    private sealed record SignalSpan(DbcMessage Message, DbcMessageItem? Item, int Start);
    private readonly List<SignalSpan> _signalSpans = [];
    private int _page = 1;
    private readonly DbcProjectionCollection<DbcSignalRow> _signalRows = [];
    private readonly DbcProjectionCollection<DbcMessageChoice> _messages = [];
    private readonly DbcProjectionCollection<DbcConfiguredMessageRow> _configuredMessages = [];
    private DbcBatchMessageSelection? _batchSelection;
    public DbcEditorViewModel(IStepPlugin plugin) : this(plugin, DbcDocumentIO.ReadAsync) { }
    internal DbcEditorViewModel(IStepPlugin plugin, Func<string, string, CancellationToken, Task<DbcDocumentSetting>> readDocument,
        Func<Action, Task>? postToEditor = null)
    {
        _plugin = plugin; _serializer = plugin.CreateSerializer(); _readDocument = readDocument;
        _postToEditor = postToEditor;
        SignalView = CollectionViewSource.GetDefaultView(SignalRows);
        SignalView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DbcSignalRow.MessageGroup)));
    }
    public bool IsWrite => _plugin is DbcWritePlugin or DbcCyclicWritePlugin;
    public bool IsRead => _plugin is DbcReadPlugin or DbcCapturePlugin or DbcReadBackgroundStartPlugin;
    public bool IsCyclic => _plugin is DbcCyclicWritePlugin;
    public bool IsCapture => _plugin is DbcCapturePlugin;
    public bool IsBackgroundRead => _plugin is DbcReadBackgroundStartPlugin;
    public bool HasTaskName => IsCyclic || IsBackgroundRead;
    public string MessageSelectionHelp => IsBackgroundRead
        ? "下方按 ID 分组显示全部已配置报文的信号，禁用报文仍可编辑配置。RC/CRC 下拉只选择保护设置，不筛选信号。总线监听按各启用 ID 独立更新最新值。"
        : "下方按 ID 分组显示全部已配置报文的信号，禁用报文仍可编辑配置。RC/CRC 下拉只选择保护设置，不筛选信号。读取等待各启用 ID 的第一帧，采集按总帧数计数。";
    public bool HasConnection => true;
    public bool HasResult => IsRead;
    public string Title => _plugin.DisplayName.Split('_').Last();
    public string? IconPath => _plugin.IconPath;
    public string DocumentName => _previewDocumentName;
    public string SourceInfo => _database == null
        ? HasFileSource ? $"外部 DBC 尚未加载：{DbcFilePath}" : "尚未加载 DBC"
        : HasFileSource
            ? $"已加载外部文件 {DocumentName}，{_database.Messages.Count} 条可用报文，编码 {_previewEncoding}；这是编辑预览，运行启动时按路径重新读取。"
            : $"已嵌入 {DocumentName}，{_database.Messages.Count} 条可用报文；运行使用保存的快照，不重新读取外部文件。";
    public IReadOnlyList<DbcLoadIssue> LoadIssues => _database?.Issues ?? Array.Empty<DbcLoadIssue>();
    public bool HasLoadIssues => LoadIssues.Count > 0;
    public string LoadIssueSummary => $"DBC 中有 {LoadIssues.Count} 条定义问题：相关报文已隔离，正确报文仍可选择；已配置 ID 不会被自动替换。";
    public bool HasFileSource => !string.IsNullOrWhiteSpace(DbcFilePath);
    public string DbcFilePath
    {
        get => _setting?.DbcFilePath ?? "";
        set
        {
            if (_setting == null || _setting.DbcFilePath == value) return;
            _setting.DbcFilePath = value;
            SourceChanged();
        }
    }
    public string DbcFileEncoding
    {
        get => _setting?.DbcFileEncoding ?? "Auto";
        set
        {
            if (_setting == null || _setting.DbcFileEncoding == value) return;
            _setting.DbcFileEncoding = value;
            // The encoding also belongs to an in-flight import/source identity.
            // Restart its preview even in snapshot mode so a discarded parse
            // cannot leave the page empty after a simultaneous encoding edit.
            SourceChanged();
        }
    }
    // Kept as the editor's import-encoding property; both controls use the same saved setting.
    public string EncodingName { get => DbcFileEncoding; set => DbcFileEncoding = value; }
    public string LoadError { get => _conflictStep != null && ReferenceEquals(_conflictStep, _step)
        ? (_loadError + " 平台已更新步骤设置，待保存的本地修改已取消，请在新设置上重新编辑。").TrimStart()
        : _loadError; private set { _loadError = value; Notify(); } }
    public bool IsLoading { get => _isLoading; private set { _isLoading = value; Notify(); Notify(nameof(CanSelect)); Notify(nameof(CanEditMessage)); Notify(nameof(CanBatchAdd)); } }
    public bool CanSelect => !IsLoading;
    public bool CanEditMessage => CanSelect && HasActiveMessage;
    public bool CanBatchAdd => _active && CanSelect && Messages.Count > 0;
    public bool HasBatchSelection => _batchSelection != null;
    public DbcBatchMessageSelection? BatchSelection => _batchSelection;
    public bool CanCaptureAll => !UseMessageList;
    public bool IsSingleMessage => !UseMessageList;
    public ObservableCollection<DbcMessageChoice> Messages => _messages;
    public ObservableCollection<DbcConfiguredMessageRow> ConfiguredMessages => _configuredMessages;
    public ObservableCollection<DbcSignalRow> SignalRows => _signalRows;
    public ICollectionView SignalView { get; }
    public int TotalSignalCount { get; private set; }
    public int SignalPageCount => (TotalSignalCount + PageSize - 1) / PageSize;
    public int SignalPageNumber => TotalSignalCount == 0 ? 0 : _page;
    public bool ShowSignalPaging => SignalPageCount > 1;
    public bool HasPreviousSignalPage => SignalPageNumber > 1;
    public bool HasNextSignalPage => SignalPageNumber < SignalPageCount;
    private string PageSummary => ShowSignalPaging ? $"共 {TotalSignalCount} 个信号，当前第 {_page}/{SignalPageCount} 页，显示 {SignalRows.Count} 个信号。" : $"显示 {SignalRows.Count} 个信号。";
    public string SignalSummary => UseMessageList
        ? $"已配置 {ConfiguredMessages.Count} 条报文；已匹配 {_signalMessageCount} 条，{PageSummary}" +
            (ConfiguredMessages.Count > _signalMessageCount ? $" {ConfiguredMessages.Count - _signalMessageCount} 条未匹配当前 DBC，未生成信号；请检查上方报文列表。" : "")
        : $"已匹配 {_signalMessageCount} 条报文，{PageSummary}";
    public void GoToSignalPage(int page)
    {
        if (!_active || SignalPageCount == 0) return;
        long identity = _editIdentity;
        Flush();
        if (!_active || identity != _editIdentity || SignalPageCount == 0) return;
        int target = Math.Clamp(page, 1, SignalPageCount);
        if (_page == target) return;
        _page = target;
        RebuildSignals(true);
    }
    public void LocateSignalMessage(DbcConfiguredMessageRow row)
    {
        if (!_active || !ConfiguredMessages.Contains(row)) return;
        var span = _signalSpans.FirstOrDefault(s => ReferenceEquals(s.Item, row.Item) && s.Message.Signals.Count != 0);
        if (span != null) GoToSignalPage(span.Start / PageSize + 1);
    }
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    private DbcMessageItem? ActiveMessage => UseMessageList ? _selectedConfiguredMessage?.Item : null;
    private ObservableCollection<DbcSignalInput>? ActiveInputs => !IsWrite ? null : UseMessageList ? ActiveMessage?.Signals : (_setting as DbcWriteSetting)?.Signals;
    private ObservableCollection<DbcSignalOutput>? ActiveOutputs => !IsRead ? null : UseMessageList ? ActiveMessage?.SignalOutputs : (_setting as DbcReadSetting)?.SignalOutputs;
    public string OutputHelp => IsBackgroundRead
        ? "后台独立监听总线，接收各 ID 的最新帧，并将选中信号覆盖写入对应标量变量；不等待全部 ID 同时到齐，也不累计历史列表。可只保留每 ID 最新 JSON、只做信号映射，或同时输出。发布间隔用于合并最新值；无新帧时变量保留原值，请通过任务状态或 JSON 判断新鲜度。使用 DBC 总线监听停止步骤的相同连接和任务名称停止。"
        : IsCapture
        ? "勾选需要读取的信号：取消勾选后，该信号不进入 JSON，也不写入变量。可只保留 JSON、只填写信号输出变量，或同时输出。每个 ID / 信号映射到已声明的列表变量（如 Locals.VoltageSamples），按接收顺序存放；无样本输出空列表。全部 DBC 采集仅支持完整 JSON，信号筛选或映射请使用指定 ID 模式。"
        : "勾选需要读取的信号：取消勾选后，该信号不进入 JSON，也不写入变量。多 ID 读取在同一个等待期限内收齐每个启用 ID 的第一帧。可只保留 JSON、只填写信号输出变量，或同时输出；每个 ID / 信号映射到已声明的数值或字符串变量（如 Locals.Voltage），留空则只进入 JSON，不自动生成变量。";
    public CanPayloadProtectionSetting? Protection => UseMessageList ? ActiveMessage?.Protection : (_setting as DbcCanSetting)?.Protection;
    public bool HasActiveMessage => !UseMessageList || ActiveMessage != null;
    public string ActiveMessageTitle => _selectedMessage is { } choice
        ? $"当前编辑：{choice.Display}" : "请先选择需要配置的报文 ID";
    public string ActiveProtectionSummary => DescribeProtection(Protection);
    public string ActiveTimingSummary => _selectedMessage is { } choice
        ? TimingSummary(choice.Message, UseDbcCycleTime, CycleTimeMs) : "请选择报文后查看发送周期来源";
    public static string DescribeProtection(CanPayloadProtectionSetting? protection)
    {
        if (protection == null) return "尚未选择报文";
        string rc = !protection.RcEnabled ? "RC：未启用" : protection.RcUseBitField
            ? $"RC：字节 {protection.RcStartByte} / bit {protection.RcBitOffset}～{protection.RcBitOffset + protection.RcBitLength - 1}"
            : $"RC：字节 {protection.RcStartByte}～{protection.RcEndByte}";
        string crc = !protection.CrcEnabled ? "CRC：未启用"
            : $"CRC：字节 {protection.CrcStartByte}～{protection.CrcEndByte} / {(protection.CrcDataIdEnabled && protection.CrcDataIdMode != CanCrcDataIdMode.Fixed ? protection.CrcDataIdMode.ToString() : protection.CrcAlgorithm.ToString())}";
        return $"{rc}；{crc}";
    }
    public DbcMessageChoice? MessageToAdd { get => _messageToAdd; set { _messageToAdd = value; Notify(); } }
    public DbcConfiguredMessageRow? SelectedConfiguredMessage
    {
        get => _selectedConfiguredMessage;
        set
        {
            if (_rebuildingMessages || _updatingActiveMessage) return;
            if (ReferenceEquals(_selectedConfiguredMessage, value)) return;
            var step = _step; long identity = _editIdentity; var source = SourceIdentity(_setting);
            int index = value == null ? -1 : (_setting as DbcCanSetting)?.Messages.IndexOf(value.Item) ?? -1;
            if (value != null && index < 0) return; // Ignore a row cached by a previous editor generation.
            uint? id = value?.Item.MessageId; var type = value?.Item.FrameType;
            Flush();
            if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step) || source != SourceIdentity(_setting)) return;
            // Saving may synchronously RefreshFromStep and recreate every row.
            // Rebind the user's selection to the current setting, never the old item.
            if (index >= 0 && (index >= ConfiguredMessages.Count || ConfiguredMessages[index].Item.MessageId != id || ConfiguredMessages[index].Item.FrameType != type)) return;
            _selectedConfiguredMessage = index < 0 ? null : ConfiguredMessages[index];
            RefreshActiveMessage(rebuildSignals: false);
        }
    }
    public bool UseMessageList
    {
        get => (_setting as DbcCanSetting)?.UseMessageList ?? false;
        set
        {
            if (_setting is not DbcCanSetting initial || initial.UseMessageList == value) return;
            var step = _step; long identity = _editIdentity; bool hadSelection = _selectedMessage != null;
            uint rootId = initial.MessageId; var rootType = initial.FrameType;
            Flush();
            if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step) || _setting is not DbcCanSetting can) return;
            if (value && can.Messages.Count == 0 && (_selectedMessage != null || hadSelection && can.MessageId == rootId && can.FrameType == rootType))
                can.Messages.Add(new DbcMessageItem { MessageId = can.MessageId, FrameType = can.FrameType, FrameFormat = can.FrameFormat,
                    Protection = can.Protection.Copy(), Signals = new((_setting as DbcWriteSetting)?.Signals.Select(CloneInput) ?? []),
                    SignalOutputs = new((_setting as DbcReadSetting)?.SignalOutputs.Select(CloneOutput) ?? []),
                    UseDbcCycleTime = (_setting as DbcCyclicWriteSetting)?.UseDbcCycleTime ?? true,
                    CycleTimeMs = (_setting as DbcCyclicWriteSetting)?.CycleTimeMs ?? 100 });
            can.UseMessageList = value;
            if (value && _setting is DbcCaptureSetting capture) capture.AllDbcMessages = false;
            RebuildMessageRows(); ObserveActive(); SelectSavedMessage(); RebuildSignals(true); Changed(string.Empty);
        }
    }
    private static DbcSignalInput CloneInput(DbcSignalInput s) => new() { Name = s.Name, Selected = s.Selected, Value = s.Value, ValueKind = s.ValueKind };
    private static DbcSignalOutput CloneOutput(DbcSignalOutput s) => new() { Name = s.Name, Selected = s.Selected, TargetVariable = s.TargetVariable, ValueKind = s.ValueKind };
    public void AddSelectedMessage()
    {
        if (MessageToAdd is { } choice) AddMessages([choice]);
    }
    public void BeginBatchSelection()
    {
        if (!CanBatchAdd || _setting is not DbcCanSetting can) return;
        _batchSelection = new(Messages, can.Messages.Select(m => (m.MessageId, m.FrameType == CanFrameType.Extended)).ToHashSet());
        Notify(nameof(BatchSelection)); Notify(nameof(HasBatchSelection));
    }
    public void BeginMessageSelection()
    {
        if (!CanBatchAdd || _setting is not DbcCanSetting can) return;
        _batchSelection = new(Messages, can.Messages.Select(m => (m.MessageId, m.FrameType == CanFrameType.Extended)).ToHashSet(), replaceSelection: true);
        Notify(nameof(BatchSelection)); Notify(nameof(HasBatchSelection));
    }
    public void ApplyMessageSelection(DbcBatchMessageSelection? selection)
    {
        if (selection == null || !selection.ReplaceSelection || !ReferenceEquals(selection, _batchSelection)
            || !_active || IsLoading || _setting is not DbcCanSetting initial) return;
        var selected = selection.Rows.Where(row => row.Selected).Select(row => row.Choice).ToArray();
        var known = selection.Rows.Select(row => (row.Choice.Message.Id, row.Choice.Message.Extended)).ToHashSet();
        var available = Messages.ToHashSet(ReferenceEqualityComparer.Instance);
        if (selection.Rows.Any(row => !available.Contains(row.Choice))) return;
        var step = _step; long identity = _editIdentity; var source = SourceIdentity(initial);
        var activeKey = _selectedConfiguredMessage == null ? ((uint, CanFrameType)?)null
            : (_selectedConfiguredMessage.Item.MessageId, _selectedConfiguredMessage.Item.FrameType);
        Flush();
        if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step)
            || _setting is not DbcCanSetting can || SourceIdentity(can) != source) return;
        var selectedKeys = selected.Select(choice => (choice.Message.Id, choice.Message.Extended)).ToHashSet();
        // A different DBC must never silently delete unmatched saved settings.
        var retained = can.Messages.Where(item => selectedKeys.Contains((item.MessageId, item.FrameType == CanFrameType.Extended))
            || !known.Contains((item.MessageId, item.FrameType == CanFrameType.Extended))).ToList();
        var existing = retained.Select(item => (item.MessageId, item.FrameType == CanFrameType.Extended)).ToHashSet();
        foreach (var choice in selected)
            if (existing.Add((choice.Message.Id, choice.Message.Extended)))
                retained.Add(new() { MessageId = choice.Message.Id, FrameType = choice.Message.Extended ? CanFrameType.Extended : CanFrameType.Standard });
        if (retained.Count > 128) { LoadError = $"最多配置 128 条报文；本次选择需要 {retained.Count} 条，原配置未修改。请减少勾选数量。"; return; }
        _batchMutation = true;
        try
        {
            can.UseMessageList = true;
            if (can is DbcCaptureSetting capture) capture.AllDbcMessages = false;
            can.Messages.Clear(); foreach (var item in retained) can.Messages.Add(item);
        }
        finally { _batchMutation = false; }
        CancelBatchSelection();
        var preferred = activeKey is { } key ? retained.FirstOrDefault(item => item.MessageId == key.Item1 && item.FrameType == key.Item2) : null;
        RefreshMessageCollection(preferred);
    }
    public void CancelBatchSelection()
    { _batchSelection = null; Notify(nameof(BatchSelection)); Notify(nameof(HasBatchSelection)); }
    public void AddBatchSelection(DbcBatchMessageSelection? selection)
    {
        if (selection == null || !ReferenceEquals(selection, _batchSelection) || !_active) return;
        if (AddMessages(selection.Rows.Where(row => row.Selected).Select(row => row.Choice))) CancelBatchSelection();
    }
    public bool AddMessages(IEnumerable<DbcMessageChoice> choices)
    {
        if (!_active || _setting is not DbcCanSetting || IsLoading) return false;
        var selected = choices.DistinctBy(c => (c.Message.Id, c.Message.Extended)).ToArray();
        if (selected.Length == 0) { LoadError = "请先勾选需要添加的报文"; return false; }
        var available = Messages.ToHashSet(ReferenceEqualityComparer.Instance);
        if (selected.Any(c => !available.Contains(c))) return false;
        var step = _step; long identity = _editIdentity; var source = SourceIdentity(_setting); bool hadRootSelection = _selectedMessage != null;
        Flush();
        if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step) || _setting is not DbcCanSetting can || SourceIdentity(can) != source) return false;
        var existing = can.Messages.Select(m => (m.MessageId, m.FrameType == CanFrameType.Extended)).ToHashSet();
        bool migrateSingle = !can.UseMessageList && can.Messages.Count == 0 && hadRootSelection;
        if (migrateSingle) existing.Add((can.MessageId, can.FrameType == CanFrameType.Extended));
        var additions = selected.Where(c => !existing.Contains((c.Message.Id, c.Message.Extended))).ToArray();
        int finalCount = can.Messages.Count + (migrateSingle ? 1 : 0) + additions.Length;
        if (finalCount > 128) { LoadError = $"最多配置 128 条报文；本批需要 {finalCount} 条，未添加任何报文。请减少勾选数量。"; return false; }
        _batchMutation = true;
        try
        {
            if (migrateSingle)
                can.Messages.Add(new() { MessageId = can.MessageId, FrameType = can.FrameType, FrameFormat = can.FrameFormat,
                    Protection = can.Protection.Copy(), Signals = new((_setting as DbcWriteSetting)?.Signals.Select(CloneInput) ?? []),
                    SignalOutputs = new((_setting as DbcReadSetting)?.SignalOutputs.Select(CloneOutput) ?? []),
                    UseDbcCycleTime = (_setting as DbcCyclicWriteSetting)?.UseDbcCycleTime ?? true,
                    CycleTimeMs = (_setting as DbcCyclicWriteSetting)?.CycleTimeMs ?? 100 });
            can.UseMessageList = true;
            if (can is DbcCaptureSetting capture) capture.AllDbcMessages = false;
            foreach (var choice in additions)
                can.Messages.Add(new() { MessageId = choice.Message.Id, FrameType = choice.Message.Extended ? CanFrameType.Extended : CanFrameType.Standard });
        }
        finally { _batchMutation = false; }
        var first = selected[0].Message;
        var preferred = can.Messages.First(item => item.MessageId == first.Id && (item.FrameType == CanFrameType.Extended) == first.Extended);
        RefreshMessageCollection(preferred);
        return true;
    }
    public void RemoveSelectedMessage()
    {
        if (_selectedConfiguredMessage is { } row) RemoveMessages([row]);
    }
    public void RemoveMessages(IEnumerable<DbcConfiguredMessageRow> rows)
    {
        if (!_active || _setting is not DbcCanSetting initial) return;
        var selected = rows.Distinct().ToArray();
        if (selected.Length == 0 || selected.Any(row => !ConfiguredMessages.Contains(row))) return;
        var identities = selected.Select(row => (Index: initial.Messages.IndexOf(row.Item), row.Item.MessageId, row.Item.FrameType)).ToArray();
        var step = _step; long identity = _editIdentity; var source = SourceIdentity(initial);
        Flush();
        if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step) || _setting is not DbcCanSetting can || SourceIdentity(can) != source) return;
        if (identities.Any(key => key.Index < 0 || key.Index >= can.Messages.Count || can.Messages[key.Index].MessageId != key.MessageId || can.Messages[key.Index].FrameType != key.FrameType)) return;
        _batchMutation = true;
        try { foreach (var key in identities.OrderByDescending(key => key.Index)) can.Messages.RemoveAt(key.Index); }
        finally { _batchMutation = false; }
        CancelBatchSelection(); RefreshMessageCollection(); QueueSave();
    }
    public void ResetReadSignalSettings()
    {
        if (_setting is not DbcReadSetting) return;
        var step = _step; long identity = _editIdentity;
        Flush();
        if (!_active || identity != _editIdentity || !ReferenceEquals(step, _step) || _setting is not DbcReadSetting read) return;
        // Explicit user action only: restore all signals and discard every saved
        // per-signal destination, including inactive single/list configurations.
        read.SignalOutputs.Clear();
        foreach (var item in read.Messages) item.SignalOutputs.Clear();
        RebuildSignals(true); QueueSave();
    }
    public string ProtectionHelp => IsWrite
        ? "先根据有效信号组帧，再应用 RC / CRC。完全位于自动字段内的信号由插件填写；配置不得覆盖复用选择器或只覆盖一个信号的部分位。周期停止可复用 CAN_Cyclic_SendStop。"
        : "读取不改写收到的数据：RC 只提取当前计数；CRC 返回检查结果。CRC 检查不代替上层合格判定，RC 跳值不直接表示设备故障。";
    public DbcMessageChoice? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (!_active || _suppress || _updatingActiveMessage || _rebuildingMessages) return;
            if (value != null && !ReferenceEquals(_choices.GetValueOrDefault((value.Message.Id, value.Message.Extended)), value)) return;
            if (ReferenceEquals(_selectedMessage, value)) return;
            // In list mode this is navigation, never an edit of another row's ID.
            if (UseMessageList)
            {
                if (value == null) SelectedConfiguredMessage = null;
                else if (ConfiguredMessages.FirstOrDefault(row => row.Item.MessageId == value.Message.Id
                    && (row.Item.FrameType == CanFrameType.Extended) == value.Message.Extended) is { } row)
                    SelectedConfiguredMessage = row;
                return;
            }
            _selectedMessage = value; Notify();
            if (value == null) { RebuildSignals(true); Notify(nameof(ActiveMessageTitle)); return; }
            if (_setting is DbcCanSetting can)
            { can.MessageId = value.Message.Id; can.FrameType = value.Message.Extended ? CanFrameType.Extended : CanFrameType.Standard; }
            RebuildSignals(false); Notify(nameof(ActiveMessageTitle)); Notify(nameof(ActiveTimingSummary)); QueueSave();
        }
    }
    internal bool IsOwnPublishedRefresh(Step step) =>
        _active && ReferenceEquals(step, _step) && _ownPublication != null &&
        ReferenceEquals(_loadedStamp, _ownPublication) && _ownPublication.Matches(step);

    public void Attach(Step step)
    {
        // The host synchronously refreshes the page from its undo/save command. Only
        // its own confirmed byte image can keep the bound rows and in-flight read.
        if (IsOwnPublishedRefresh(step)) return;
        var previousStep = _step;
        (uint Id, CanFrameType Type)? activeKey = ReferenceEquals(step, _step)
            ? _selectedConfiguredMessage != null ? (_selectedConfiguredMessage.Item.MessageId, _selectedConfiguredMessage.Item.FrameType) : _suspendedSelection
            : null;
        _attachFlushDepth++;
        try { Flush(); }
        finally { _attachFlushDepth--; }
        // A newer step selection from a synchronous save callback takes priority.
        // A same-step refresh is allowed: the requested next step still wins.
        if (!ReferenceEquals(_step, previousStep) && !ReferenceEquals(_step, step)) return;
        // A fresh authoritative binding invalidates gestures begun against the
        // previous object, even when the same step and DBC source were retained.
        _editIdentity++;
        if (!ReferenceEquals(_step, step)) { _conflictStep = null; _page = 1; }
        _suspendedSelection = null;
        CancelLoad(); DetachObservers(); _step = step; _active = true;
        var stepSetting = step.StepSetting;
        byte[]? bytes = stepSetting.Setting;
        int version = stepSetting.SettingVersion;
        _setting = bytes is { Length: > 0 }
            ? (DbcSourceSetting)_serializer.Deserialize(bytes, version)
            : (DbcSourceSetting)_serializer.CreateDefault();
        _loadedStamp = SettingStamp.Capture(version, bytes);
        _setting.Document ??= new();
        if (_setting is DbcCanSetting can) can.Messages ??= [];
        _database = null; _previewDocumentName = ""; _previewEncoding = "";
        _choices.Clear(); _messages.ReplaceWith([]); _signalRowsGeneration++; _signalMessageCount = 0; TotalSignalCount = 0; _signalSpans.Clear(); _signalRows.ReplaceWith([]); _selectedMessage = null; _selectedConfiguredMessage = null; _messageToAdd = null;
        Observe(); RebuildMessageRows();
        if (activeKey is { } key)
            _selectedConfiguredMessage = ConfiguredMessages.FirstOrDefault(row => row.Item.MessageId == key.Item1 && row.Item.FrameType == key.Item2)
                ?? ConfiguredMessages.FirstOrDefault();
        ObserveActive();
        LoadError = ""; Notify(string.Empty);
        LoadTask = LoadSourceAsync(invalidatePreview: false);
    }
    public void Flush() => _save.Flush();
    public void Activate()
    {
        // A cached WPF page may load again without RefreshFromStep. Rehydrate
        // the current step, never revive detached rows or a cancelled DBC load.
        // Initial RefreshFromStep already attached it; do not restart that load.
        if (!_active && _step is { } step) Attach(step);
    }
    public void Deactivate()
    {
        if (!_active) return;
        Flush();
        _suspendedSelection = _selectedConfiguredMessage is { } row ? (row.Item.MessageId, row.Item.FrameType) : null;
        _active = false; _editIdentity++; _signalRowsGeneration++;
        CancelLoad(); DetachObservers();
        // The host can cache an unloaded page. Rows and settings are projections of
        // the saved step, not owners of a catalog or a reason to keep its text alive.
        _setting = null; _loadedStamp = null; _database = null; _choices = [];
        _selectedMessage = null; _selectedConfiguredMessage = null; _messageToAdd = null; _signalMessageCount = 0;
        TotalSignalCount = 0; _signalSpans.Clear();
        _previewDocumentName = ""; _previewEncoding = ""; _loadError = "";
        _messages.ReplaceWith([]); _configuredMessages.ReplaceWith([]); _signalRows.ReplaceWith([]);
        LoadTask = Task.CompletedTask;
        Notify(string.Empty);
    }
    private void CancelLoad()
    {
        CancelBatchSelection();
        _generation++; var old = _loadStop; _loadStop = null; old?.Cancel(); IsLoading = false;
    }
    public Task LoadFileAsync(string path)
    {
        string encoding = EncodingName;
        LoadTask = LoadAsync(token => _readDocument(path, encoding, token), false);
        return LoadTask;
    }
    public Task ReloadSourceAsync() { LoadTask = LoadSourceAsync(); return LoadTask; }
    public Task ReloadEmbeddedAsync() => ReloadSourceAsync();
    private static (string Path, string Encoding, string Text) SourceIdentity(DbcSourceSetting? setting)
        => (setting?.DbcFilePath ?? "", setting?.DbcFileEncoding ?? "Auto", setting?.Document?.Text ?? "");
    private void SourceChanged()
    {
        _page = 1;
        QueueSave();
        LoadTask = LoadSourceAsync(debounce: true);
    }
    private void InvalidatePreview()
    {
        bool suppressed = _suppress, updating = _updatingActiveMessage;
        _suppress = true; _updatingActiveMessage = true;
        try
        {
            _database = null; _previewDocumentName = ""; _previewEncoding = "";
            _choices.Clear(); _messages.ReplaceWith([]); _selectedMessage = null; _messageToAdd = null;
            RebuildSignals(true);
            foreach (var row in ConfiguredMessages) row.Refresh();
            LoadError = ""; Notify(string.Empty);
        }
        finally { _suppress = suppressed; _updatingActiveMessage = updating; }
    }
    private Task LoadSourceAsync(bool debounce = false, bool invalidatePreview = true)
    {
        CancelLoad(); if (invalidatePreview) InvalidatePreview();
        if (!_active || _setting == null) return Task.CompletedTask;
        if (HasFileSource)
        {
            string path = DbcFilePath, encoding = DbcFileEncoding;
            return LoadAsync(token => _readDocument(DbcDocumentIO.ValidateFileSource(path, encoding), encoding, token),
                preserveInputs: true, externalPreview: true, debounce: debounce);
        }
        var source = _setting?.Document;
        if (source == null || string.IsNullOrWhiteSpace(source.Text)) return Task.CompletedTask;
        // Capture text independently of a later UI setting replacement.
        var copy = new DbcDocumentSetting { Name = source.Name, Text = source.Text, Encoding = source.Encoding };
        return LoadAsync(_ => Task.FromResult(copy), true, debounce: debounce);
    }
    private async Task LoadAsync(Func<CancellationToken, Task<DbcDocumentSetting>> read, bool preserveInputs,
        bool externalPreview = false, bool debounce = false)
    {
        CancelLoad();
        if (_step == null || _setting == null) return;
        var step = _step; var setting = _setting; long generation = _generation;
        var sourceIdentity = SourceIdentity(setting);
        int timeoutMs = setting.TimeoutMs;
        var stop = new CancellationTokenSource(); _loadStop = stop;
        IsLoading = true; LoadError = "";
        try
        {
            if (timeoutMs <= 0) throw new ArgumentException("加载等待超时必须大于 0 ms");
            var token = stop.Token;
            if (debounce) await Task.Delay(200, token).ConfigureAwait(false);
            stop.CancelAfter(timeoutMs);
            var reading = read(token);
            DbcDocumentSetting document;
            try { document = await reading.WaitAsync(token).ConfigureAwait(false); }
            catch { _ = reading.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); throw; }
            var (parsed, _) = await DbcSharedCatalog.LoadAsync(document, token).ConfigureAwait(false);
            // Prepare the read-only selectors before touching WPF. Publishing their
            // collection sends one reset regardless of the number of messages.
            var choices = await Task.Run(() =>
            {
                var index = new Dictionary<(uint, bool), DbcMessageChoice>(parsed.Messages.Count);
                foreach (var message in parsed.Messages)
                {
                    token.ThrowIfCancellationRequested();
                    index.Add((message.Id, message.Extended), new(message));
                }
                return (Index: index, Ordered: index.Values.ToArray());
            }, token).ConfigureAwait(false);
            await OnEditorAsync(() =>
            {
                if (!_active || generation != _generation || !ReferenceEquals(step, _step) || !ReferenceEquals(setting, _setting)
                    || sourceIdentity != SourceIdentity(_setting) || token.IsCancellationRequested) return;
                _suppress = true;
                try
                {
                    bool hadSource = !string.IsNullOrWhiteSpace(setting.Document?.Text)
                        || !string.IsNullOrWhiteSpace(setting.DbcFilePath);
                    // Importing a catalog is not choosing a frame. In particular, the initial
                    // single-frame ID 0 must not silently select a real ID 0 in the DBC.
                    if (!preserveInputs && !hadSource && setting is DbcCanSetting { UseMessageList: false } can &&
                        can.Messages.Count == 0 && setting is not DbcCaptureSetting { AllDbcMessages: true } && !HasSingleMessageConfiguration(can))
                        can.UseMessageList = true;
                    // External files are authoritative inputs, not a request to rewrite an
                    // embedded snapshot. Import is an explicit, atomic source switch.
                    if (!preserveInputs) { setting.Document = document; setting.DbcFilePath = ""; }
                    _database = parsed; _previewDocumentName = document.Name; _previewEncoding = document.Encoding;
                    _choices = choices.Index; _messages.ReplaceWith(choices.Ordered);
                    RebuildMessageRows(); SelectSavedMessage(); _messageToAdd = null;
                    if (_selectedMessage != null && !UseMessageList) LoadError = "";
                    // Import changes the document, not the user's configured frame
                    // or saved signals. Only selecting a different frame resets them.
                    ObserveActive(); RebuildSignals(true); Notify(string.Empty);
                }
                finally { _suppress = false; }
                // Loading the original embedded document must not rewrite saved settings.
                if (!preserveInputs) QueueSave();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            await OnEditorAsync(() => { if (generation == _generation) LoadError = externalPreview
                ? "外部 DBC 加载已超时或取消，未使用内嵌快照；请确认文件大小与等待期限。"
                : "DBC 加载已超时或取消，保留先前已选文档；请确认文件大小与等待期限。"; }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await OnEditorAsync(() => { if (generation == _generation) LoadError = externalPreview
                ? $"外部 DBC 加载失败：{ex.Message}。未使用内嵌快照，请修正文件路径或编码。"
                : $"DBC 加载失败：{ex.Message}。保留先前已选文档，请勿将其视为新文件已加载。"; }).ConfigureAwait(false);
        }
        finally
        {
            await OnEditorAsync(() => { if (generation == _generation) { IsLoading = false; _loadStop = null; } }).ConfigureAwait(false);
            stop.Dispose();
        }
    }
    private Task OnEditorAsync(Action action)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return Task.CompletedTask;
        if (_dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
        return _postToEditor is null ? _dispatcher.InvokeAsync(action).Task : _postToEditor(action);
    }
    private static readonly Lazy<string> DefaultProtection = new(() => new CanPayloadProtectionSetting().ToString());
    private static bool HasSingleMessageConfiguration(DbcCanSetting setting)
    {
        // An untouched single-ID 0 is indistinguishable from an unselected ID.
        // Keep 0 when its signal mapping, format, protection or timing records intent.
        return setting.MessageId != 0 || setting.FrameType != CanFrameType.Standard || setting.FrameFormat != DbcFrameFormat.Auto ||
            setting is DbcWriteSetting { Signals.Count: > 0 } or DbcReadSetting { SignalOutputs.Count: > 0 } ||
            setting is DbcCyclicWriteSetting cyclic && (!cyclic.UseDbcCycleTime || cyclic.CycleTimeMs != 100) ||
            setting.Protection != null && !string.Equals(setting.Protection.ToString(), DefaultProtection.Value, StringComparison.Ordinal);
    }
    private void RebuildSignals(bool preserveInputs)
    {
        bool previous = _changingSignalCollection;
        _changingSignalCollection = true;
        try { RebuildSignalsCore(preserveInputs); }
        finally { _changingSignalCollection = previous; }
    }
    private void RegisterSignal(Action register)
    {
        bool previous = _changingSignalCollection;
        _changingSignalCollection = true;
        try { register(); }
        finally { _changingSignalCollection = previous; }
    }
    private void RebuildSignalsCore(bool preserveInputs)
    {
        _signalRowsGeneration++; _signalSpans.Clear(); TotalSignalCount = 0;
        if (UseMessageList && _setting is DbcCanSetting can)
        {
            foreach (var item in can.Messages)
            {
                if (FindMessage(item) is not { } message) continue;
                _signalSpans.Add(new(message, item, TotalSignalCount));
                TotalSignalCount += message.Signals.Count;
            }
        }
        else if (_selectedMessage is { } choice)
        {
            _signalSpans.Add(new(choice.Message, null, 0));
            TotalSignalCount = choice.Message.Signals.Count;
            // Switching a single message resets the former message's selections.
            // Paging within it always preserves all settings, including off-page edits.
            if (!preserveInputs) { _page = 1; ActiveInputs?.Clear(); ActiveOutputs?.Clear(); }
        }
        _signalMessageCount = _signalSpans.Count;
        if (SignalPageCount != 0) _page = Math.Clamp(_page, 1, SignalPageCount);
        int start = (_page - 1) * PageSize, end = start + PageSize;
        var rows = new List<DbcSignalRow>(Math.Min(PageSize, TotalSignalCount));
        foreach (var span in _signalSpans)
        {
            if (span.Start >= end) break;
            int first = Math.Max(0, start - span.Start), last = Math.Min(span.Message.Signals.Count, end - span.Start);
            if (first >= last) continue;
            var item = span.Item;
            AddSignalRows(rows, span.Message, first, last, IsWrite ? item?.Signals ?? ActiveInputs : null,
                IsRead ? item?.SignalOutputs ?? ActiveOutputs : null, item?.Protection ?? Protection, item?.Enabled ?? true,
                seedInputs: item == null && !preserveInputs && SignalPageCount == 1);
        }
        _signalRows.ReplaceWith(rows);
        Notify(nameof(SignalSummary)); Notify(nameof(TotalSignalCount)); Notify(nameof(SignalPageCount)); Notify(nameof(SignalPageNumber));
        Notify(nameof(ShowSignalPaging)); Notify(nameof(HasPreviousSignalPage)); Notify(nameof(HasNextSignalPage));
    }
    private static Dictionary<string, T?> PageSettings<T>(DbcMessage message, int first, int last,
        IEnumerable<T> configured, Func<T, string> getName) where T : class
    {
        // Only index names needed by this page. Indexing the entire DBC would
        // allocate one entry per signal just to expose a single editable page.
        var result = new Dictionary<string, T?>(last - first, StringComparer.Ordinal);
        for (int i = first; i < last; i++) result.Add(message.Signals[i].Name, null);
        int missing = result.Count;
        foreach (var setting in configured)
        {
            string name = getName(setting);
            if (name != null && result.TryGetValue(name, out var found) && found == null)
            {
                result[name] = setting;
                if (--missing == 0) break;
            }
        }
        return result;
    }

    private void AddSignalRows(List<DbcSignalRow> rows, DbcMessage message, int first, int last, ObservableCollection<DbcSignalInput>? inputs,
        ObservableCollection<DbcSignalOutput>? outputs, CanPayloadProtectionSetting? protection, bool enabled, bool seedInputs)
    {
        var setting = _setting; var step = _step; long generation = _signalRowsGeneration;
        bool CanModify() => _active && generation == _signalRowsGeneration && ReferenceEquals(setting, _setting) && ReferenceEquals(step, _step);
        Func<bool> canModify = CanModify;
        string display = _choices[(message.Id, message.Extended)].Display;
        string group = DbcSignalRow.GroupLabel(display, enabled);
        if (inputs != null)
        {
            var byName = PageSettings(message, first, last, inputs, i => i.Name);
            for (int i = first; i < last; i++)
            {
                var signal = message.Signals[i];
                if (byName[signal.Name] is { } saved)
                    rows.Add(new(signal, saved, protection, message: message, messageEnabled: enabled, canModify: canModify, messageDisplay: display, messageGroup: group));
                else
                {
                    var missing = DefaultInput(signal);
                    if (seedInputs) inputs.Add(missing);
                    rows.Add(new(signal, missing, protection, seedInputs ? null : () => RegisterSignal(() => inputs.Add(missing)),
                        message: message, messageEnabled: enabled, canModify: canModify, messageDisplay: display, messageGroup: group));
                }
            }
        }
        else if (outputs != null)
        {
            var byName = PageSettings(message, first, last, outputs, o => o.Name);
            for (int i = first; i < last; i++)
            {
                var signal = message.Signals[i];
                if (byName[signal.Name] is { } saved)
                    rows.Add(new(signal, null, protection, output: saved, message: message, messageEnabled: enabled, canModify: canModify, messageDisplay: display, messageGroup: group));
                else
                {
                    var missing = new DbcSignalOutput { Name = signal.Name };
                    rows.Add(new(signal, null, protection, output: missing, registerOutput: () => RegisterSignal(() => outputs.Add(missing)),
                        message: message, messageEnabled: enabled, canModify: canModify, messageDisplay: display, messageGroup: group));
                }
            }
        }
        else for (int i = first; i < last; i++)
            rows.Add(new(message.Signals[i], null, protection, message: message, messageEnabled: enabled, canModify: canModify, messageDisplay: display, messageGroup: group));
    }
    private static DbcSignalInput DefaultInput(DbcSignal signal)
    {
        try
        {
            return new() { Name = signal.Name, Selected = false,
                Value = checked(signal.InitialRawValue * signal.Factor + signal.Offset).ToString(CultureInfo.InvariantCulture), ValueKind = DbcValueKind.Physical };
        }
        catch (OverflowException)
        { return new() { Name = signal.Name, Selected = false, Value = signal.InitialRawValue.ToString(CultureInfo.InvariantCulture), ValueKind = DbcValueKind.Raw }; }
    }
    private void RebuildMessageRows(DbcMessageItem? selected = null)
    {
        _rebuildingMessages = true;
        try
        {
            selected ??= _selectedConfiguredMessage?.Item;
            var rows = _setting is DbcCanSetting can ? can.Messages.Select(item =>
                new DbcConfiguredMessageRow(item, MessageDisplay, MessageTimingSummary, row => FindMessage(row)?.CycleTimeMs)).ToArray() : [];
            _configuredMessages.ReplaceWith(rows);
            _selectedConfiguredMessage = rows.FirstOrDefault(row => ReferenceEquals(row.Item, selected)) ?? rows.FirstOrDefault();
            Notify(nameof(SelectedConfiguredMessage)); Notify(nameof(HasActiveMessage));
        }
        finally { _rebuildingMessages = false; }
    }
    private string MessageDisplay(DbcMessageItem item)
    {
        if (_choices.TryGetValue((item.MessageId, item.FrameType == CanFrameType.Extended), out var choice)) return choice.Display;
        var issue = FindLoadIssue(item.MessageId, item.FrameType);
        return issue != null ? $"0x{item.MessageId:X} [{item.FrameType}]（定义已隔离：{issue.MessageName}，第 {issue.Line} 行：{issue.Reason}）"
            : $"0x{item.MessageId:X} [{item.FrameType}]（{(_database == null ? "等待加载 DBC" : "此 DBC 中不存在")}）";
    }
    private DbcLoadIssue? FindLoadIssue(uint id, CanFrameType type) => LoadIssues.FirstOrDefault(issue =>
        issue.RawMessageId == (type == CanFrameType.Extended ? id | 0x80000000u : id));
    private DbcMessage? FindMessage(DbcMessageItem item) => _choices.GetValueOrDefault((item.MessageId, item.FrameType == CanFrameType.Extended))?.Message;
    private string MessageTimingSummary(DbcMessageItem item) => FindMessage(item) is { } message
        ? TimingSummary(message, item.UseDbcCycleTime, item.CycleTimeMs) : "等待匹配 DBC";
    private static string TimingSummary(DbcMessage message, bool useDbc, int manualPeriod)
    {
        try
        {
            int period = DbcCycleTiming.Resolve(message, useDbc, manualPeriod);
            return $"{period} ms · {(useDbc ? message.CycleTimeMs.HasValue ? message.CycleTimeSource : "DBC 未声明有效周期，使用缺省值" : "人工设置")}";
        }
        catch (ArgumentException ex) { return $"周期无效：{ex.Message}"; }
    }
    private void SelectSavedMessage()
    {
        if (_setting is not DbcCanSetting can) return;
        uint id = ActiveMessage?.MessageId ?? can.MessageId;
        var type = ActiveMessage?.FrameType ?? can.FrameType;
        _selectedMessage = UseMessageList && ActiveMessage == null ? null
            : _choices.GetValueOrDefault((id, type == CanFrameType.Extended));
        if (_database != null && _selectedMessage == null && HasActiveMessage)
            LoadError = FindLoadIssue(id, type) is { } issue
                ? $"保存的报文定义已隔离：{issue.DisplayText}。原 ID 及信号配置已保留，修正定义后重新加载。"
                : $"保存的报文 0x{id:X}（{type}）不在此 DBC 中，请明确选择新的报文；未自动替换原 ID。";
        else LoadError = "";
        if (_database != null && UseMessageList && can.Messages.Any(item => FindMessage(item) == null))
            LoadError = "列表中存在当前 DBC 未定义或已隔离的报文，原配置已保留；请查看报文行及定义问题列表，修正后重新加载或明确移除，未自动替换。";
        Notify(nameof(SelectedMessage));
    }
    private void Observe()
    {
        if (_setting is DbcCanSetting can)
        {
            can.Protection ??= new(); _observedMessages = can.Messages;
            _observedMessages.CollectionChanged += MessagesChanged; ObserveMessageItems();
        }
        if (_setting is DbcWriteSetting write) write.Signals ??= [];
        if (_setting is DbcReadSetting read) read.SignalOutputs ??= [];
    }
    private void ObserveActive()
    {
        DetachInputs(); DetachOutputs();
        foreach (var protection in _observedProtections) protection.PropertyChanged -= ProtectionChanged;
        _observedProtections.Clear();
        if (UseMessageList && _setting is DbcCanSetting can)
        {
            foreach (var item in can.Messages)
            {
                ObserveProtection(item.Protection);
                if (IsWrite) ObserveInputs(item.Signals);
                if (IsRead) ObserveOutputs(item.SignalOutputs);
            }
        }
        else
        {
            if (Protection is { } protection) ObserveProtection(protection);
            if (ActiveInputs is { } inputs) ObserveInputs(inputs);
            if (ActiveOutputs is { } outputs) ObserveOutputs(outputs);
        }
    }
    private void ObserveProtection(CanPayloadProtectionSetting protection)
    { if (_observedProtections.Add(protection)) protection.PropertyChanged += ProtectionChanged; }
    private void ObserveMessageItems()
    {
        foreach (var item in _observedMessageItems) item.PropertyChanged -= MessageChanged;
        _observedMessageItems.Clear();
        if (_observedMessages == null) return;
        foreach (var item in _observedMessages)
            if (_observedMessageItems.Add(item)) { item.Protection ??= new(); item.Signals ??= []; item.SignalOutputs ??= []; item.PropertyChanged += MessageChanged; }
    }
    private void MessagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_batchMutation) return;
        CancelBatchSelection(); RefreshMessageCollection();
    }
    private void RefreshMessageCollection(DbcMessageItem? preferred = null)
    {
        ObserveMessageItems(); RebuildMessageRows(preferred); RefreshActiveMessage(); QueueSave();
    }
    private void RefreshActiveMessage(bool rebuildSignals = true)
    {
        _updatingActiveMessage = true;
        try { if (rebuildSignals) ObserveActive(); SelectSavedMessage(); if (rebuildSignals) RebuildSignals(true); Notify(string.Empty); }
        finally { _updatingActiveMessage = false; }
    }
    private void MessageChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var row in ConfiguredMessages) if (ReferenceEquals(row.Item, sender)) row.Refresh();
        if (args.PropertyName is nameof(DbcMessageItem.Protection) or nameof(DbcMessageItem.Signals) or nameof(DbcMessageItem.SignalOutputs))
        { ObserveActive(); RebuildSignals(true); Notify(string.Empty); }
        if (args.PropertyName is nameof(DbcMessageItem.MessageId) or nameof(DbcMessageItem.FrameType) or nameof(DbcMessageItem.Enabled))
            RefreshActiveMessage();
        if (ReferenceEquals(sender, ActiveMessage) && args.PropertyName is nameof(DbcMessageItem.CycleTimeMs) or nameof(DbcMessageItem.UseDbcCycleTime))
        { Notify(nameof(CycleTimeMs)); Notify(nameof(UseDbcCycleTime)); Notify(nameof(ActiveTimingSummary)); }
        if (ReferenceEquals(sender, ActiveMessage) && args.PropertyName == nameof(DbcMessageItem.FrameFormat))
            Notify(nameof(FrameFormat));
        QueueSave();
    }
    private void ObserveInputs(ObservableCollection<DbcSignalInput> collection)
    {
        if (!_observedInputs.Add(collection)) return;
        collection.CollectionChanged += InputsChanged;
        foreach (var input in collection) if (_observedRows.Add(input)) input.PropertyChanged += InputChanged;
    }
    private void DetachInputs()
    {
        foreach (var collection in _observedInputs) collection.CollectionChanged -= InputsChanged;
        foreach (var input in _observedRows) input.PropertyChanged -= InputChanged;
        _observedRows.Clear(); _observedInputs.Clear();
    }
    private void DetachObservers()
    {
        DetachInputs(); DetachOutputs();
        foreach (var protection in _observedProtections) protection.PropertyChanged -= ProtectionChanged;
        _observedProtections.Clear();
        if (_observedMessages != null) _observedMessages.CollectionChanged -= MessagesChanged;
        foreach (var item in _observedMessageItems) item.PropertyChanged -= MessageChanged;
        _observedMessageItems.Clear(); _observedMessages = null;
    }
    private void SignalChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Renames change the DBC projection. Other edits refresh only cells
        // referencing this exact source, including same-named signals across IDs.
        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(DbcSignalInput.Name))
            RebuildSignals(true);
        else foreach (var row in SignalRows) if (row.UsesSignal(sender)) row.RefreshProtection();
        QueueSave();
    }
    private void InputChanged(object? sender, PropertyChangedEventArgs args) => SignalChanged(sender, args);
    private void InputsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Add && args.NewItems != null)
        {
            foreach (DbcSignalInput input in args.NewItems)
                if (_observedRows.Add(input)) input.PropertyChanged += InputChanged;
        }
        else ObserveActive();
        if (!_changingSignalCollection) RebuildSignals(true);
        QueueSave();
    }
    private void ObserveOutputs(ObservableCollection<DbcSignalOutput> collection)
    {
        if (!_observedOutputs.Add(collection)) return;
        collection.CollectionChanged += OutputsChanged;
        foreach (var output in collection) if (_observedOutputRows.Add(output)) output.PropertyChanged += OutputChanged;
    }
    private void DetachOutputs()
    {
        foreach (var collection in _observedOutputs) collection.CollectionChanged -= OutputsChanged;
        foreach (var output in _observedOutputRows) output.PropertyChanged -= OutputChanged;
        _observedOutputRows.Clear(); _observedOutputs.Clear();
    }
    private void OutputChanged(object? sender, PropertyChangedEventArgs args) => SignalChanged(sender, args);
    private void OutputsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Add && args.NewItems != null)
        {
            foreach (DbcSignalOutput output in args.NewItems)
                if (_observedOutputRows.Add(output)) output.PropertyChanged += OutputChanged;
        }
        else ObserveActive();
        if (!_changingSignalCollection) RebuildSignals(true);
        QueueSave();
    }
    private void ProtectionChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var row in SignalRows) if (row.UsesProtection(sender)) row.RefreshProtection();
        foreach (var row in ConfiguredMessages) if (ReferenceEquals(row.Item.Protection, sender)) row.Refresh();
        Notify(nameof(ActiveProtectionSummary)); QueueSave();
    }
    private void QueueSave()
    {
        if (!_active || _suppress || _step == null || _setting == null || _loadedStamp == null) return;
        if (_conflictStep != null) { _conflictStep = null; Notify(nameof(LoadError)); }
        var step = _step; var setting = _setting; var expected = _loadedStamp; var command = ExecuteCommand;
        _save.Queue(() =>
        {
            var outer = _ownPublication;
            try
            {
                void Publish()
                {
                    if (!CanPublishEdit(step, expected)) return;
                    var bytes = _serializer.Serialize(setting);
                    var stamp = SettingStamp.Capture(_serializer.SettingVersion, bytes);
                    _ownPublication = stamp;
                    if (ReferenceEquals(setting, _setting) && ReferenceEquals(step, _step)) _loadedStamp = stamp;
                    step.StepSetting.Setting = bytes;
                    step.StepSetting.SettingVersion = stamp.Version;
                }
                if (command == null) Publish();
                else if (CanPublishEdit(step, expected)) command("修改步骤参数", Publish);
            }
            finally { _ownPublication = outer; }
        });
    }
    private bool CanPublishEdit(Step step, SettingStamp expected)
    {
        if (expected.Matches(step)) return true;
        // An assistant/undo action already replaced the setting before debounce
        // expired. Its authoritative bytes win. Never overwrite them with stale UI.
        if (ReferenceEquals(step, _step) && _active)
        {
            _conflictStep = step;
            if (_attachFlushDepth == 0) Attach(step);
            Notify(nameof(LoadError));
        }
        return false;
    }
    private void Changed([CallerMemberName] string? name = null) { Notify(name); QueueSave(); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting != null && _setting.TimeoutMs != value) { _setting.TimeoutMs = value; Changed(); } } }
    public string CatalogQuery { get => _setting?.CatalogQuery ?? ""; set { if (_setting != null && _setting.CatalogQuery != value) { _setting.CatalogQuery = value; Changed(); } } }
    public string ConnectionName { get => (_setting as DbcCanSetting)?.ConnectionName ?? ""; set { if (_setting is DbcCanSetting s && s.ConnectionName != value) { s.ConnectionName = value; Changed(); } } }
    public int FrameFormat
    {
        get => (int)(ActiveMessage?.FrameFormat ?? (_setting as DbcCanSetting)?.FrameFormat ?? DbcFrameFormat.Auto);
        set
        {
            if (ActiveMessage is { } item && (int)item.FrameFormat != value) { item.FrameFormat = (DbcFrameFormat)value; Changed(); }
            else if (!UseMessageList && _setting is DbcCanSetting s && (int)s.FrameFormat != value) { s.FrameFormat = (DbcFrameFormat)value; Changed(); }
        }
    }
    public string TaskName
    {
        get => _setting switch { DbcCyclicWriteSetting s => s.TaskName, DbcReadBackgroundSetting s => s.TaskName, _ => "" };
        set
        {
            if (_setting is DbcCyclicWriteSetting cyclic && cyclic.TaskName != value) { cyclic.TaskName = value; Changed(); }
            else if (_setting is DbcReadBackgroundSetting background && background.TaskName != value) { background.TaskName = value; Changed(); }
        }
    }
    public int PublishIntervalMs { get => (_setting as DbcReadBackgroundSetting)?.PublishIntervalMs ?? 20; set { if (_setting is DbcReadBackgroundSetting s && s.PublishIntervalMs != value) { s.PublishIntervalMs = value; Changed(); } } }
    public int MaxAgeMs { get => (_setting as DbcReadBackgroundSetting)?.MaxAgeMs ?? 1000; set { if (_setting is DbcReadBackgroundSetting s && s.MaxAgeMs != value) { s.MaxAgeMs = value; Changed(); } } }
    public int CycleTimeMs
    {
        get => ActiveMessage?.CycleTimeMs ?? (_setting as DbcCyclicWriteSetting)?.CycleTimeMs ?? 100;
        set
        {
            if (ActiveMessage is { } item) { if (item.CycleTimeMs != value) item.CycleTimeMs = value; }
            else if (!UseMessageList && _setting is DbcCyclicWriteSetting s && s.CycleTimeMs != value) { s.CycleTimeMs = value; Changed(); }
            Notify(nameof(ActiveTimingSummary));
        }
    }
    public bool UseDbcCycleTime
    {
        get => ActiveMessage?.UseDbcCycleTime ?? (_setting as DbcCyclicWriteSetting)?.UseDbcCycleTime ?? true;
        set
        {
            if (UseDbcCycleTime == value) return;
            if (!value && _selectedMessage?.Message.CycleTimeMs is { } period) CycleTimeMs = period;
            if (ActiveMessage is { } item) item.UseDbcCycleTime = value;
            else if (!UseMessageList && _setting is DbcCyclicWriteSetting s) { s.UseDbcCycleTime = value; Changed(); }
            Notify(nameof(ActiveTimingSummary));
        }
    }
    public int DurationMs { get => (_setting as DbcCaptureSetting)?.DurationMs ?? 1000; set { if (_setting is DbcCaptureSetting s && s.DurationMs != value) { s.DurationMs = value; Changed(); } } }
    public int MaxFrames { get => (_setting as DbcCaptureSetting)?.MaxFrames ?? 10000; set { if (_setting is DbcCaptureSetting s && s.MaxFrames != value) { s.MaxFrames = value; Changed(); } } }
    public bool AllDbcMessages { get => (_setting as DbcCaptureSetting)?.AllDbcMessages ?? false; set { if (_setting is DbcCaptureSetting s && s.AllDbcMessages != value && (!value || CanCaptureAll)) { s.AllDbcMessages = value; Changed(); } } }
    public string ResultVariable
    {
        get => (_setting as DbcReadSetting)?.ResultVariable ?? "";
        set { if (_setting is DbcReadSetting read && read.ResultVariable != value) { read.ResultVariable = value; Changed(); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
