using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CAN.Dbc;

/// <summary>DBC 整数信号目录。支持普通/简单复用信号；未实现的布局扩展在导入时明确拒绝。</summary>
public sealed class DbcDatabase
{
    public const int MaximumTextCharacters = 8 * 1024 * 1024;
    public IReadOnlyList<DbcMessage> Messages { get; }
    public IReadOnlyList<DbcLoadIssue> Issues { get; }
    private readonly IReadOnlyDictionary<(uint, bool), DbcMessage> _byId;
    private DbcDatabase(IEnumerable<DbcMessage> messages, IEnumerable<DbcLoadIssue>? issues = null)
    {
        Messages = Array.AsReadOnly(messages.ToArray());
        Issues = Array.AsReadOnly((issues ?? []).ToArray());
        _byId = Messages.ToDictionary(m => (m.Id, m.Extended));
    }

    public DbcMessage Find(uint id, bool extended) => _byId.TryGetValue((id, extended), out var message) ? message
        : throw new KeyNotFoundException(Issues.FirstOrDefault(i => i.RawMessageId == (extended ? id | 0x80000000u : id)) is { } issue
            ? $"DBC 报文已隔离，不能用于读写：{issue.DisplayText}"
            : $"DBC 没有 {(extended ? "扩展" : "标准")} ID 0x{id:X}");

    public static DbcDatabase Parse(string text, CancellationToken ct = default)
        => ParseCore(text, false, ct);

    /// <summary>隔离损坏的整条报文，保留其他完整定义。全局语法边界和资源限制仍严格校验。</summary>
    public static DbcDatabase ParseCatalog(string text, CancellationToken ct = default)
        => ParseCore(text, true, ct);

    private static DbcDatabase ParseCore(string text, bool tolerant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextCharacters) throw new ArgumentException("DBC 文本不能超过 8 Mi 字符");
        ct.ThrowIfCancellationRequested();
        return new Parser(ct, tolerant).Parse(text);
    }

    private sealed class MessageBuilder(uint rawId, string name, int length, int line)
    {
        internal uint RawId { get; } = rawId;
        internal string Name { get; } = name;
        internal int Length { get; } = length;
        internal int Line { get; } = line;
        internal List<SignalBuilder> Signals { get; } = [];
        internal HashSet<string> SignalNames { get; } = new(StringComparer.Ordinal);
    }
    private sealed record SignalBuilder(string Name, int Start, int Length, bool Little, bool Signed,
        decimal Factor, decimal Offset, decimal Min, decimal Max, string Unit, bool Multiplexer, decimal? MuxValue, int Line);

    private sealed class LimitException(string message) : FormatException(message);

    private sealed class Parser(CancellationToken ct, bool tolerant)
    {
        private const string Identifier = @"[A-Za-z_][A-Za-z_0-9]*";
        private const string Quoted = "\"((?:\\\\.|[^\"\\\\])*)\"";
        private readonly Dictionary<uint, MessageBuilder> _messages = [];
        private readonly HashSet<string> _messageNames = new(StringComparer.Ordinal);
        private readonly Dictionary<(uint, string), string> _comments = [];
        private readonly Dictionary<(uint, string), Dictionary<decimal, string>> _values = [];
        private readonly Dictionary<(uint, string), decimal> _initialValues = [];
        private readonly Dictionary<uint, int> _messageCycles = [];
        private readonly Dictionary<(uint, string), int> _signalCycles = [];
        private readonly Dictionary<string, int> _defaultCycles = new(StringComparer.Ordinal);
        private readonly HashSet<string> _cycleDefinitions = new(StringComparer.Ordinal);
        private decimal? _defaultInitialValue;
        private bool _initialValueDefinitionSeen;
        private readonly Dictionary<uint, string> _formats = [];
        private readonly List<(uint Id, string Signal)> _integerTypes = [];
        private string[]? _formatNames;
        private string? _defaultFormat, _busType, _defaultBusType;
        private int _line, _signalCount;
        private MessageBuilder? _current;
        private bool _independent;
        private readonly List<DbcLoadIssue> _issues = [];
        private readonly HashSet<uint> _isolated = [];
        private readonly HashSet<uint> _seenIds = [];
        private readonly Dictionary<string, uint> _seenNames = new(StringComparer.Ordinal);
        private readonly Dictionary<(uint Id, string Signal), int> _referenceLines = [];
        private readonly Dictionary<(uint Id, string Key), int> _attributeLines = [];
        private uint? _blockId;
        private int _statementLine;
        private bool _namespaceDeclaration;
        private readonly Dictionary<string, int> _globalLines = new(StringComparer.Ordinal);

        internal DbcDatabase Parse(string text)
        {
            using var reader = new StringReader(text);
            var pending = new StringBuilder(); bool quoted = false, escaped = false, hasStatementText = false;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                ct.ThrowIfCancellationRequested();
                if (++_line > 250_000 || line.Length > 65536) throw Limit("DBC 行数或单行长度超过解析上限");
                line = line.Trim().TrimStart('\uFEFF').TrimStart();
                if (pending.Length == 0)
                {
                    if (Match(line, @"^NS_\s*:").Success) { _namespaceDeclaration = true; continue; }
                    if (_namespaceDeclaration)
                    {
                        if (line.Length == 0 || Match(line, $@"^{Identifier}$").Success) continue;
                        _namespaceDeclaration = false;
                    }
                }
                if (pending.Length > 0 || IsStatement(line))
                {
                    foreach (char c in line)
                    {
                        if (!hasStatementText && !char.IsWhiteSpace(c)) _statementLine = _line;
                        pending.Append(c);
                        if (!char.IsWhiteSpace(c)) hasStatementText = true;
                        if (pending.Length > 1024 * 1024) throw Limit("DBC 单条元数据语句超过 1 Mi 字符");
                        if (escaped) { escaped = false; continue; }
                        if (quoted && c == '\\') { escaped = true; continue; }
                        if (c == '"') quoted = !quoted;
                        if (c == ';' && !quoted)
                        {
                            ProcessStatement(pending.ToString().Trim()); pending.Clear(); hasStatementText = false;
                        }
                    }
                    if (!hasStatementText) pending.Clear();
                    else pending.Append('\n');
                    continue;
                }
                if (line == "BO_" || line.StartsWith("BO_", StringComparison.Ordinal) && HasPayload(line, "BO_")) ProcessMessage(line);
                else if (line == "SG_" || line.StartsWith("SG_", StringComparison.Ordinal) && HasPayload(line, "SG_"))
                {
                    if (tolerant && _blockId.HasValue && _isolated.Contains(_blockId.Value)) continue;
                    Scoped(() => ParseSignal(line), _blockId, _current?.Name, allowUnscoped: true);
                }
            }
            if (pending.Length > 0 || quoted) throw Error("DBC 元数据语句或引号未结束");
            if (_messages.Count == 0 && _issues.Count == 0) throw Error("DBC 没有可用报文");
            // A corrupt shared default cannot be attributed to one message.
            if (_defaultFormat != null)
            {
                _line = _globalLines["Default:VFrameFormat"];
                _ = FormatName(_defaultFormat);
            }
            foreach (var key in _comments.Keys.Concat(_values.Keys).Concat(_initialValues.Keys).Concat(_signalCycles.Keys).Concat(_integerTypes.Select(t => (t.Id, t.Signal))))
            {
                ct.ThrowIfCancellationRequested();
                if (!_messages.TryGetValue(key.Item1, out var message) || !message.SignalNames.Contains(key.Item2))
                {
                    _line = _referenceLines.GetValueOrDefault(key, _line);
                    Scoped(() => throw Error($"元数据引用未知报文或信号 {key.Item1}/{key.Item2}"), key.Item1, message?.Name);
                }
            }
            foreach (uint id in _formats.Keys) CheckMessageReference(id, "VFrameFormat");
            foreach (uint id in _messageCycles.Keys) CheckMessageReference(id, "GenMsgCycleTime");
            var usable = new List<DbcMessage>();
            foreach (var source in _messages.Values)
            {
                ct.ThrowIfCancellationRequested();
                if (_isolated.Contains(source.RawId)) continue;
                _line = source.Line;
                Scoped(() => usable.Add(Build(source)), source.RawId, source.Name);
            }
            return new DbcDatabase(usable, _issues.Select(issue => issue.MessageName == null && issue.RawMessageId is { } id &&
                _messages.TryGetValue(id, out var source) ? issue with { MessageName = source.Name } : issue));
        }

        private void CheckMessageReference(uint id, string attribute)
        {
            if (_messages.ContainsKey(id)) return;
            _line = _attributeLines.GetValueOrDefault((id, attribute), _line);
            Scoped(() => throw Error($"{attribute} 引用未知报文 {id}"), id, null);
        }

        private void Scoped(Action parse, uint? id, string? name, bool allowUnscoped = false)
        {
            try { parse(); }
            catch (Exception ex) when (tolerant && (id.HasValue || allowUnscoped) &&
                ex is not LimitException && (ex is FormatException or ArgumentException or NotSupportedException))
            {
                Isolate(id, name, ex.Message);
            }
        }

        private void Isolate(uint? id, string? name, string reason)
        {
            ct.ThrowIfCancellationRequested();
            if (_issues.Count >= 1024) throw Limit("DBC 问题数超过 1024，无法可靠加载，请先修复文件");
            if (id.HasValue)
            {
                _isolated.Add(id.Value);
                if (name == null && _messages.TryGetValue(id.Value, out var message)) name = message.Name;
            }
            // Diagnostics are bounded independently of the source text and never contain full statements.
            if (reason.Length > 1024) reason = reason[..1024] + "…";
            _issues.Add(new(_line, id, name, reason));
        }

        private void ProcessMessage(string line)
        {
            // A malformed BO_ must never attach following SG_ records to the previous valid message.
            _current = null; _independent = false; _blockId = null;
            var header = Match(line, @"^BO_\s+(\d+)(?=\s|:|$)(?:\s+([^\s:]+))?");
            string? name = header.Success && header.Groups[2].Success ? header.Groups[2].Value : null;
            if (header.Success && uint.TryParse(header.Groups[1].Value, out uint raw)) _blockId = raw;
            if (tolerant && _blockId is { } id && id != 0xC0000000)
            {
                if (_seenIds.Count >= 10_000 && !_seenIds.Contains(id)) throw Limit("DBC 报文数超过 10000");
                if (!_seenIds.Add(id)) Isolate(id, name, "DBC 报文 ID/类型重复，所有同 ID 定义均已隔离");
                if (name != null && !_seenNames.TryAdd(name, id) && _seenNames[name] != id)
                {
                    Isolate(_seenNames[name], name, "DBC 报文名称重复，相关 ID 均已隔离");
                    Isolate(id, name, "DBC 报文名称重复，相关 ID 均已隔离");
                }
                if (_isolated.Contains(id)) return;
            }
            Scoped(() => ParseMessage(line), _blockId, name, allowUnscoped: true);
        }

        private void ProcessStatement(string statement)
        {
            int endLine = _line; _line = _statementLine;
            try
            {
                uint? id = null; string? signal = null; string? attribute = null;
                var target = Match(statement, @"^(?:SIG_VALTYPE_|SG_MUL_VAL_|SIG_TYPE_REF_|VAL_)\s+(\d+)(?=\s|;|$)(?:\s+([^\s;]+))?");
                if (!target.Success) target = Match(statement, @"^CM_\s+(?:SG_|BO_)\s+(\d+)(?=\s|;|$)(?:\s+([^\s"";]+))?");
                var assignment = Match(statement, "^BA_\\s+\"([^\"]+)\"\\s+(BO_|SG_)\\s+(\\d+)(?=\\s|;|$)(?:\\s+([^\\s;]+))?");
                if (assignment.Success)
                {
                    if (uint.TryParse(assignment.Groups[3].Value, out uint raw)) id = raw;
                    attribute = assignment.Groups[1].Value;
                    if (assignment.Groups[2].Value == "SG_") signal = assignment.Groups[4].Value;
                    // BusType is a shared layout property even when a corrupt statement
                    // incorrectly supplies a message/signal scope.
                    if (attribute == "BusType") id = null;
                }
                else if (target.Success)
                {
                    if (uint.TryParse(target.Groups[1].Value, out uint raw)) id = raw;
                    if (target.Groups[2].Success) signal = target.Groups[2].Value;
                    attribute = statement.Split(' ', '\t')[0];
                }
                if (id is { } key)
                {
                    if (signal != null) _referenceLines[(key, signal)] = _line;
                    if (attribute != null) _attributeLines[(key, attribute + (signal == null ? "" : ":" + signal))] = _line;
                }
                Scoped(() => ParseStatement(statement), id, null);
            }
            finally { _line = endLine; }
        }

        private static bool HasPayload(string line, string keyword) => line.Length > keyword.Length && char.IsWhiteSpace(line[keyword.Length]);
        private static bool IsStatement(string line) => new[] { "CM_", "VAL_", "VAL_TABLE_", "BA_", "BA_DEF_", "BA_DEF_DEF_", "SIG_VALTYPE_", "SG_MUL_VAL_", "SIG_TYPE_REF_", "SGTYPE_", "SIGTYPE_VALTYPE_" }
            .Any(keyword => line.StartsWith(keyword, StringComparison.Ordinal) && HasPayload(line, keyword));
        private Match Match(string line, string pattern) => Regex.Match(line, pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(200));
        private FormatException Error(string message) => new($"DBC 第 {_line} 行附近：{message}");
        private LimitException Limit(string message) => new($"DBC 第 {_line} 行附近：{message}");
        private void RegisterGlobal(string key)
        {
            if (!_globalLines.TryAdd(key, _line)) throw Error($"全局属性 {key} 重复，首次位于第 {_globalLines[key]} 行");
        }
        private uint UInt(string text) => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ? value : throw Error("无效无符号整数");
        private int Int(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : throw Error("整数过大");
        private decimal Number(string text)
        {
            try { return DbcNumber.ParseExact(text); }
            catch (FormatException ex) { throw new FormatException($"DBC 第 {_line} 行附近：{ex.Message}", ex); }
        }

        private void ParseMessage(string line)
        {
            var m = Match(line, $@"^BO_\s+(\d+)\s+({Identifier})\s*:\s*(\d+)\s+\S+\s*$");
            if (!m.Success) throw Error("BO_ 报文格式无效");
            uint raw = UInt(m.Groups[1].Value); string name = m.Groups[2].Value; int length = Int(m.Groups[3].Value);
            _independent = raw == 0xC0000000 && name == "VECTOR__INDEPENDENT_SIG_MSG" && length == 0;
            if (_independent) { _current = null; return; }
            bool extended = (raw & 0x80000000) != 0; uint id = raw & 0x7FFFFFFF;
            if (id > (extended ? 0x1FFFFFFF : 0x7FF) || length > 64) throw Error("报文 ID 或长度无效，载荷上限 64 字节");
            if (_messages.Count >= 10_000) throw Limit("DBC 报文数超过 10000");
            if (_messages.ContainsKey(raw) || !_messageNames.Add(name)) throw Error("DBC 报文 ID/类型或名称重复");
            _current = new(raw, name, length, _line); _messages.Add(raw, _current);
        }

        private void ParseSignal(string line)
        {
            if (_independent) return;
            if (_current == null) throw Error("SG_ 信号之前没有 BO_ 报文");
            var m = Match(line, $@"^SG_\s+({Identifier})\s*(.*?)\s*:\s*(\d+)\|(\d+)@([01])([+-])\s*\(([^,]+),([^\)]+)\)\s*\[([^|]+)\|([^\]]+)\]\s*{Quoted}(?:\s+[^;]+)?\s*$");
            if (!m.Success) throw Error("SG_ 信号格式无效");
            string mux = m.Groups[2].Value.Trim(); bool selector = mux == "M"; decimal? muxValue = null;
            if (!selector && mux.Length > 0)
            {
                if (!Match(mux, @"^m\d+$").Success) throw new NotSupportedException($"DBC 第 {_line} 行使用未支持的扩展复用 {mux}");
                muxValue = Number(mux[1..]);
            }
            int start = Int(m.Groups[3].Value), length = Int(m.Groups[4].Value);
            if (length is < 1 or > 64 || start >= _current.Length * 8) throw Error("信号位宽必须为 1～64 且起始位必须在报文内");
            decimal factor = Number(m.Groups[7].Value), offset = Number(m.Groups[8].Value), min = Number(m.Groups[9].Value), max = Number(m.Groups[10].Value);
            if (factor == 0 || min > max) throw Error("信号缩放不能为 0，最小值不能大于最大值");
            if (++_signalCount > 100_000 || _current.Signals.Count >= 4096) throw Limit("DBC 信号数量超过解析上限");
            var signal = new SignalBuilder(m.Groups[1].Value, start, length, m.Groups[5].Value == "1", m.Groups[6].Value == "-",
                factor, offset, min, max, Unquote(m.Groups[11].Value), selector, muxValue, _line);
            if (!_current.SignalNames.Add(signal.Name)) throw Error($"信号名重复：{signal.Name}");
            _current.Signals.Add(signal);
        }

        private void ParseStatement(string line)
        {
            if (ParseCycleStatement(line)) return;
            if (line.StartsWith("SG_MUL_VAL_", StringComparison.Ordinal) || line.StartsWith("SIG_TYPE_REF_", StringComparison.Ordinal) ||
                line.StartsWith("SGTYPE_", StringComparison.Ordinal) || line.StartsWith("SIGTYPE_VALTYPE_", StringComparison.Ordinal))
                throw new NotSupportedException($"DBC 第 {_line} 行使用未支持的复用或信号布局扩展：{line.Split(' ', '\t')[0]}");
            if (line.StartsWith("SIG_VALTYPE_", StringComparison.Ordinal))
            {
                var m = Match(line, $@"^SIG_VALTYPE_\s+(\d+)\s+({Identifier})\s*:\s*(\d+)\s*;$");
                if (!m.Success) throw Error("SIG_VALTYPE_ 格式无效");
                if (UInt(m.Groups[1].Value) == 0xC0000000) return;
                if (UInt(m.Groups[3].Value) != 0) throw new NotSupportedException($"DBC 第 {_line} 行使用浮点信号，目前仅支持整数原始信号");
                _integerTypes.Add((UInt(m.Groups[1].Value), m.Groups[2].Value)); return;
            }
            if (line.StartsWith("CM_", StringComparison.Ordinal))
            {
                var m = Match(line, $@"^CM_\s+SG_\s+(\d+)\s+({Identifier})\s+{Quoted}\s*;$");
                if (m.Success && UInt(m.Groups[1].Value) != 0xC0000000) _comments[(UInt(m.Groups[1].Value), m.Groups[2].Value)] = Unquote(m.Groups[3].Value);
                else if (line.StartsWith("CM_ SG_", StringComparison.Ordinal) && !m.Success) throw Error("信号注释格式无效");
                return;
            }
            if (line.StartsWith("VAL_", StringComparison.Ordinal) && HasPayload(line, "VAL_"))
            {
                var m = Match(line, $@"^VAL_\s+(\d+)\s+({Identifier})\s+(.*);$");
                if (!m.Success)
                {
                    // Environment-variable value descriptions do not change CAN signal layout.
                    if (Match(line, $@"^VAL_\s+{Identifier}\s+.*;$").Success) return;
                    throw Error("VAL_ 信号值枚举格式无效");
                }
                uint id = UInt(m.Groups[1].Value); if (id == 0xC0000000) return;
                var values = new Dictionary<decimal, string>(); string pairs = m.Groups[3].Value.Trim();
                while (pairs.Length > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var pair = Match(pairs, $@"^([+-]?\d+)\s+{Quoted}\s*");
                    if (!pair.Success || !values.TryAdd(Number(pair.Groups[1].Value), Unquote(pair.Groups[2].Value))) throw Error("VAL_ 数值重复或格式无效");
                    if (values.Count > 4096) throw Limit("单信号值枚举数超过 4096");
                    pairs = pairs[pair.Length..];
                }
                if (!_values.TryAdd((id, m.Groups[2].Value), values)) throw Error("同一信号存在多条 VAL_ 定义"); return;
            }
            if (line.StartsWith("BA_DEF_", StringComparison.Ordinal) && HasPayload(line, "BA_DEF_"))
            {
                if (Match(line, @"^BA_DEF_\s+(?:(?:BU_|BO_|SG_|EV_)\s+)?""GenSigStartValue""").Success)
                {
                    var definition = Match(line, @"^BA_DEF_\s+SG_\s+""GenSigStartValue""\s+(?:INT|HEX|FLOAT)\s+(\S+)\s+(\S+)\s*;$");
                    if (!definition.Success) throw Error("GenSigStartValue 必须使用 SG_ 数值属性定义");
                    if (_initialValueDefinitionSeen) throw Error("GenSigStartValue 属性定义重复");
                    if (Number(definition.Groups[1].Value) > Number(definition.Groups[2].Value))
                        throw Error("GenSigStartValue 属性最小值不能大于最大值");
                    _initialValueDefinitionSeen = true;
                    return;
                }
                var m = Match(line, "^BA_DEF_\\s+BO_\\s+\"VFrameFormat\"\\s+ENUM\\s+(.+);$");
                if (m.Success)
                {
                    RegisterGlobal("Definition:VFrameFormat");
                    string remaining = m.Groups[1].Value.Trim(); var names = new List<string>();
                    while (remaining.Length > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        var item = Match(remaining, $@"^{Quoted}\s*(?:,\s*|$)");
                        if (!item.Success) throw Error("VFrameFormat ENUM 格式无效");
                        names.Add(Unquote(item.Groups[1].Value)); remaining = remaining[item.Length..];
                        if (names.Count > 4096) throw Limit("VFrameFormat 枚举数超过 4096");
                    }
                    _formatNames = names.ToArray();
                }
                else if (line.Contains("\"VFrameFormat\"", StringComparison.Ordinal)) throw Error("VFrameFormat 必须使用 BO_ ENUM 定义");
                return;
            }
            if (line.StartsWith("BA_DEF_DEF_", StringComparison.Ordinal))
            {
                if (Match(line, @"^BA_DEF_DEF_\s+""GenSigStartValue""").Success)
                {
                    var initial = Match(line, @"^BA_DEF_DEF_\s+""GenSigStartValue""\s+(\S+)\s*;$");
                    if (!initial.Success) throw Error("GenSigStartValue 属性默认值格式无效");
                    if (_defaultInitialValue.HasValue) throw Error("GenSigStartValue 属性默认值重复");
                    _defaultInitialValue = ParseInitialValue(initial.Groups[1].Value);
                    return;
                }
                var m = Match(line, "^BA_DEF_DEF_\\s+\"VFrameFormat\"\\s+(.+?)\\s*;$");
                if (m.Success) { RegisterGlobal("Default:VFrameFormat"); _defaultFormat = m.Groups[1].Value.Trim(); }
                else if (Match(line, @"^BA_DEF_DEF_\s+""VFrameFormat""").Success) throw Error("VFrameFormat 属性默认值格式无效");
                var bus = Match(line, $@"^BA_DEF_DEF_\s+""BusType""\s+{Quoted}\s*;$");
                if (bus.Success) { RegisterGlobal("Default:BusType"); _defaultBusType = Unquote(bus.Groups[1].Value); }
                else if (Match(line, @"^BA_DEF_DEF_\s+""BusType""").Success) throw Error("BusType 属性默认值格式无效");
                return;
            }
            if (line.StartsWith("BA_", StringComparison.Ordinal) && HasPayload(line, "BA_"))
            {
                if (Match(line, @"^BA_\s+""GenSigStartValue""").Success)
                {
                    var initial = Match(line, $@"^BA_\s+""GenSigStartValue""\s+SG_\s+(\d+)\s+({Identifier})\s+(\S+)\s*;$");
                    if (!initial.Success) throw Error("GenSigStartValue 信号属性格式无效");
                    uint id = UInt(initial.Groups[1].Value);
                    if (id == 0xC0000000) return;
                    decimal value = ParseInitialValue(initial.Groups[3].Value);
                    if (!_initialValues.TryAdd((id, initial.Groups[2].Value), value))
                        throw Error("同一信号存在多条 GenSigStartValue 定义");
                    return;
                }
                var format = Match(line, "^BA_\\s+\"VFrameFormat\"\\s+BO_\\s+(\\d+)\\s+(.+?)\\s*;$");
                if (format.Success)
                {
                    uint id = UInt(format.Groups[1].Value);
                    if (id != 0xC0000000) _formats[id] = format.Groups[2].Value.Trim();
                    return;
                }
                var bus = Match(line, $@"^BA_\s+""BusType""\s+{Quoted}\s*;$");
                if (bus.Success) { RegisterGlobal("Value:BusType"); _busType = Unquote(bus.Groups[1].Value); }
                else if (line.Contains("\"VFrameFormat\"", StringComparison.Ordinal) || line.Contains("\"BusType\"", StringComparison.Ordinal))
                    throw Error("VFrameFormat 或 BusType 属性格式无效");
            }
        }

        private decimal ParseInitialValue(string text)
        {
            decimal value = Number(text);
            if (value != decimal.Truncate(value)) throw Error("GenSigStartValue 必须是整数原始值，不能是物理值或小数");
            return value;
        }

        private int ParseCycleValue(string text)
        {
            decimal value = Number(text);
            if (value != decimal.Truncate(value) || value < 0 || value > int.MaxValue)
                throw Error("DBC 周期属性必须是 0～2147483647 的整数毫秒；0 表示未声明有效周期");
            return (int)value;
        }

        private bool ParseCycleStatement(string line)
        {
            var prefix = Match(line, @"^(BA_DEF_|BA_DEF_DEF_|BA_)\s+(?:(?:BU_|BO_|SG_|EV_)\s+)?""(GenMsgCycleTime|GenSigCycleTime)""");
            if (!prefix.Success) return false;
            string keyword = prefix.Groups[1].Value, name = prefix.Groups[2].Value;
            string scope = name == "GenMsgCycleTime" ? "BO_" : "SG_";
            if (keyword == "BA_DEF_")
            {
                var definition = Match(line, $@"^BA_DEF_\s+{scope}\s+""{name}""\s+(?:INT|HEX|FLOAT)\s+(\S+)\s+(\S+)\s*;$");
                if (!definition.Success || !_cycleDefinitions.Add(name)) throw Error($"{name} 属性定义格式无效或重复");
                if (Number(definition.Groups[1].Value) > Number(definition.Groups[2].Value)) throw Error($"{name} 属性范围无效");
                return true;
            }
            if (keyword == "BA_DEF_DEF_")
            {
                var value = Match(line, $@"^BA_DEF_DEF_\s+""{name}""\s+(\S+)\s*;$");
                if (!value.Success || !_defaultCycles.TryAdd(name, ParseCycleValue(value.Groups[1].Value)))
                    throw Error($"{name} 默认值格式无效或重复");
                return true;
            }
            string signalPart = scope == "SG_" ? $@"\s+({Identifier})" : "";
            var assignment = Match(line, $@"^BA_\s+""{name}""\s+{scope}\s+(\d+){signalPart}\s+(\S+)\s*;$");
            if (!assignment.Success) throw Error($"{name} 属性赋值格式无效");
            uint id = UInt(assignment.Groups[1].Value);
            if (id == 0xC0000000) return true;
            bool added = scope == "SG_"
                ? _signalCycles.TryAdd((id, assignment.Groups[2].Value), ParseCycleValue(assignment.Groups[3].Value))
                : _messageCycles.TryAdd(id, ParseCycleValue(assignment.Groups[2].Value));
            if (!added) throw Error($"{name} 同一目标属性重复");
            return true;
        }

        private (int? Period, string Source) ResolveCycle(MessageBuilder message)
        {
            bool explicitMessage = _messageCycles.TryGetValue(message.RawId, out int period);
            if (!explicitMessage) period = _defaultCycles.GetValueOrDefault("GenMsgCycleTime");
            if (period > 0) return (period, explicitMessage ? "GenMsgCycleTime 报文属性" : "GenMsgCycleTime 属性默认值");
            var signalPeriods = message.Signals.Select(s => _signalCycles.TryGetValue((message.RawId, s.Name), out int value)
                ? value : _defaultCycles.GetValueOrDefault("GenSigCycleTime")).Where(value => value > 0).ToArray();
            return signalPeriods.Length > 0
                ? (signalPeriods.Min(), "GenSigCycleTime 最短有效信号周期（整帧发送）")
                : (null, "未声明有效周期");
        }

        private DbcMessage Build(MessageBuilder source)
        {
            ct.ThrowIfCancellationRequested();
            _line = source.Line;
            var signalLines = source.Signals.ToDictionary(s => s.Name, s => s.Line, StringComparer.Ordinal);
            var signals = source.Signals.Select(s => new DbcSignal(s.Name, s.Start, s.Length, s.Little, s.Signed, s.Factor,
                s.Offset, s.Min, s.Max, s.Unit, _comments.GetValueOrDefault((source.RawId, s.Name)) ?? "", s.Multiplexer,
                s.MuxValue, _values.GetValueOrDefault((source.RawId, s.Name)) ?? [],
                _initialValues.TryGetValue((source.RawId, s.Name), out decimal initial) ? initial : _defaultInitialValue)).ToArray();
            var selectors = signals.Where(s => s.IsMultiplexer).ToArray();
            if (selectors.Length > 1) throw Error($"报文 {source.Name} 仅支持一个复用选择信号");
            foreach (var signal in signals)
            {
                ct.ThrowIfCancellationRequested();
                _line = signalLines[signal.Name];
                if (signal.BitPositions().Any(b => b < 0 || b >= source.Length * 8)) throw Error($"信号 {signal.Name} 位域超出报文长度");
                if (signal.HasInitialValue)
                {
                    _line = _attributeLines.GetValueOrDefault((source.RawId, "GenSigStartValue:" + signal.Name), _line);
                    try { signal.ToRaw(signal.InitialRawValue, true); }
                    catch (ArgumentException ex)
                    {
                        throw new FormatException($"DBC 第 {_line} 行附近：报文 {source.Name} 信号 {signal.Name} 的 GenSigStartValue 无效：{ex.Message}", ex);
                    }
                }
                if (signal.MultiplexerValue is { } branch)
                {
                    _line = signalLines[signal.Name];
                    if (selectors.Length == 0) throw Error($"信号 {signal.Name} 使用复用分支但报文没有选择信号");
                    selectors[0].ToRaw(branch, true);
                }
                _line = _attributeLines.GetValueOrDefault((source.RawId, "VAL_:" + signal.Name), signalLines[signal.Name]);
                foreach (decimal value in signal.ValueDescriptions.Keys) signal.ToRaw(value, true);
            }
            var unconditional = new Dictionary<int, string>();
            var any = new Dictionary<int, string>();
            var branches = new Dictionary<decimal, Dictionary<int, string>>();
            foreach (var signal in signals)
                foreach (int bit in signal.BitPositions())
                {
                    ct.ThrowIfCancellationRequested();
                    _line = signalLines[signal.Name];
                    if (signal.MultiplexerValue is { } branch)
                    {
                        if (!branches.TryGetValue(branch, out var branchBits)) branches.Add(branch, branchBits = []);
                        if (unconditional.TryGetValue(bit, out var other) || branchBits.TryGetValue(bit, out other))
                            throw Error($"信号 {signal.Name}/{other} 的活动位域重叠");
                        branchBits.Add(bit, signal.Name);
                    }
                    else
                    {
                        if (any.TryGetValue(bit, out var other)) throw Error($"信号 {signal.Name}/{other} 的活动位域重叠");
                        unconditional.Add(bit, signal.Name);
                    }
                    any.TryAdd(bit, signal.Name);
                }
            bool extended = (source.RawId & 0x80000000) != 0;
            _line = _attributeLines.GetValueOrDefault((source.RawId, "VFrameFormat"), source.Line);
            string? format = _formats.GetValueOrDefault(source.RawId) ?? _defaultFormat;
            bool fd = IsFd(source, extended, format);
            var cycle = ResolveCycle(source);
            return new(source.RawId & 0x1FFFFFFF, extended, source.Name, source.Length, fd, signals, cycle.Period, cycle.Source);
        }

        private bool IsFd(MessageBuilder message, bool extended, string? format)
        {
            if (format == null) return message.Length > 8 || string.Equals((_busType ?? _defaultBusType)?.Replace("_", " "), "CAN FD", StringComparison.OrdinalIgnoreCase);
            string name = FormatName(format);
            bool fd = name.EndsWith("CAN_FD", StringComparison.OrdinalIgnoreCase);
            bool standard = name.Equals("StandardCAN", StringComparison.OrdinalIgnoreCase) || name.Equals("StandardCAN_FD", StringComparison.OrdinalIgnoreCase);
            if (_formats.ContainsKey(message.RawId) && standard == extended) throw Error($"报文 {message.Name} 的 VFrameFormat 与 ID 扩展标志冲突");
            if (!fd && message.Length > 8) throw Error($"报文 {message.Name} 的经典帧标记与长度冲突");
            return fd;
        }

        private string FormatName(string format)
        {
            string name;
            if (format.StartsWith('"') && format.EndsWith('"')) name = Unquote(format[1..^1]);
            else
            {
                int index = Int(format);
                if (_formatNames != null)
                { if (index >= _formatNames.Length) throw Error("VFrameFormat 枚举值不存在"); name = _formatNames[index]; }
                else name = index switch { 0 => "StandardCAN", 1 => "ExtendedCAN", 3 => "J1939PG", 14 => "StandardCAN_FD", 15 => "ExtendedCAN_FD", _ => throw Error("未知 VFrameFormat 值") };
            }
            bool standard = name.Equals("StandardCAN", StringComparison.OrdinalIgnoreCase) || name.Equals("StandardCAN_FD", StringComparison.OrdinalIgnoreCase);
            bool extendedName = name.Equals("ExtendedCAN", StringComparison.OrdinalIgnoreCase) || name.Equals("ExtendedCAN_FD", StringComparison.OrdinalIgnoreCase) || name.Equals("J1939PG", StringComparison.OrdinalIgnoreCase);
            if (!standard && !extendedName) throw Error($"未支持的 VFrameFormat：{name}");
            return name;
        }

        private static string Unquote(string value)
        {
            var result = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char next = value[++i];
                    if (next is '"' or '\\') result.Append(next);
                    else if (next == 'n') result.Append('\n');
                    else if (next == 'r') result.Append('\r');
                    else if (next == 't') result.Append('\t');
                    else { result.Append('\\'); result.Append(next); }
                }
                else result.Append(c);
            }
            return result.ToString();
        }
    }
}
