using CAN.Models;
using xTestPlatform.Core.Engine;

namespace CAN.Dbc;

/// <summary>One selected identity from the connection's latest-frame observer; never a FIFO drain.</summary>
internal sealed record DbcBackgroundFrame(uint Id, CanFrameType FrameType, bool IsFd,
    CanMessage? Frame, long Sequence, bool Valid, string Quality, double? AgeUpperBoundMs)
{
    internal (uint Id, CanFrameType Type, bool Fd) Identity => (Id, FrameType, IsFd);
}

/// <summary>Prepares complete variable updates before the publication fence mutates platform state.</summary>
internal sealed class DbcReadBackgroundMapping
{
    private sealed record Binding(string Name, string Path, DbcValueKind Kind, Type Target);
    private sealed record Cached(long Sequence, object Frame, IReadOnlyDictionary<string, DbcDecodedSignal> Signals);
    private readonly DbcSelectedMessage[] _messages;
    private readonly Dictionary<(uint, CanFrameType, bool), Binding[]> _bindings = new();
    private readonly Dictionary<(uint, CanFrameType, bool), Cached> _latest = new();
    private readonly Dictionary<(uint, CanFrameType, bool), long> _published = new();
    private readonly IExecutionContext _context;
    private readonly string _jsonPath, _fingerprint;
    internal IReadOnlyCollection<string> Destinations { get; }

    internal DbcReadBackgroundMapping(DbcReadBackgroundSetting setting, DbcSelectedMessage[] messages,
        string fingerprint, IExecutionContext context)
    {
        if (setting.PublishIntervalMs <= 0 || setting.MaxAgeMs <= 0)
            throw new ArgumentException("DBC 后台读取的发布间隔和最新值有效期必须大于 0 ms");
        _context = context; _messages = messages; _fingerprint = fingerprint; _jsonPath = setting.ResultVariable;
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_jsonPath))
        {
            DbcVariableBinding.ValidateJsonTarget(_jsonPath, context);
            destinations.Add(_jsonPath);
        }
        foreach (var message in messages)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var bindings = new List<Binding>();
            foreach (var output in message.Outputs)
            {
                if (!message.Message.Signals.Any(s => s.Name == output.Name) || !names.Add(output.Name))
                    throw new ArgumentException($"0x{message.Message.Id:X} 输出映射包含未知或重复信号：{output.Name}");
                if (!output.Selected || string.IsNullOrWhiteSpace(output.TargetVariable)) continue;
                if (!Enum.IsDefined(output.ValueKind)) throw new ArgumentException($"信号 {output.Name} 的输出值类型无效");
                if (!destinations.Add(output.TargetVariable)) throw new ArgumentException($"输出变量重复（含 JSON 结果）：{output.TargetVariable}");
                bindings.Add(new(output.Name, output.TargetVariable, output.ValueKind,
                    DbcVariableBinding.TargetType(output.TargetVariable, context, capture: false)));
            }
            _bindings.Add(message.Identity, bindings.ToArray());
        }
        if (destinations.Count == 0) throw new ArgumentException("请配置 JSON 结果变量或至少一个信号输出变量");
        Destinations = destinations.ToArray();
    }

    internal async Task<Dictionary<string, object?>> PrepareAsync(IReadOnlyList<DbcBackgroundFrame> samples, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var observed = samples.ToDictionary(s => s.Identity);
        var pending = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var frames = new List<object>();
        foreach (var message in _messages)
        {
            ct.ThrowIfCancellationRequested();
            observed.TryGetValue(message.Identity, out var sample);
            var identity = message.Identity;
            _latest.TryGetValue(identity, out var cached);
            bool hasFrame = sample?.Frame != null;
            if (hasFrame && (cached == null || cached.Sequence != sample!.Sequence))
            {
                // Decode and inspect protection even for stale arrivals; malformed frames are faults, not silent gaps.
                var decoded = message.Message.Decode(sample!.Frame!.Data);
                cached = new(sample.Sequence, DbcPayload.DecodeFrame(message.Message, sample.Frame, message.Protection,
                    message.FilterSignals(decoded)), decoded.ToDictionary(s => s.Name, StringComparer.Ordinal));
                _latest[identity] = cached;
            }
            bool fresh = hasFrame && sample!.Valid;
            bool publishable = fresh || hasFrame && sample!.Quality == "TimeUnknown";
            bool changed = publishable && (!_published.TryGetValue(identity, out long sequence) || sequence != sample!.Sequence);
            var mappings = new List<object>();
            foreach (var binding in _bindings[identity])
            {
                ct.ThrowIfCancellationRequested();
                DbcDecodedSignal? signal = null;
                bool active = cached?.Signals.TryGetValue(binding.Name, out signal) == true;
                if (changed && active)
                {
                    if (DbcVariableBinding.TargetType(binding.Path, _context, capture: false) != binding.Target)
                        throw new InvalidOperationException($"后台输出变量 {binding.Path} 类型发生变化，未发布本次结果");
                    try
                    {
                        decimal value = binding.Kind == DbcValueKind.Raw ? signal!.RawValue : signal!.PhysicalValue;
                        pending.Add(binding.Path, DbcVariableBinding.ConvertValues([value], binding.Target, capture: false, binding.Kind));
                    }
                    catch (Exception ex) { throw new InvalidOperationException($"0x{identity.Id:X} 信号 {binding.Name} 无法写入 {binding.Path}：{ex.Message}", ex); }
                }
                mappings.Add(new { name = binding.Name, targetVariable = binding.Path, active,
                    updated = changed && active,
                    reason = !hasFrame ? "NoData" : !publishable ? sample!.Quality : !active ? "InactiveMultiplexer" :
                        !fresh ? "TimeUnknown" : changed ? "Updated" : "Unchanged" });
            }
            frames.Add(new { id = $"0x{identity.Id:X}", frameType = identity.Type.ToString(), isFd = identity.Fd,
                sequence = sample?.Sequence ?? 0, valid = fresh, quality = sample?.Quality ?? "NoData",
                ageUpperBoundMs = sample?.AgeUpperBoundMs, frame = cached?.Frame, mappings });
        }
        if (!string.IsNullOrWhiteSpace(_jsonPath))
        {
            DbcVariableBinding.ValidateJsonTarget(_jsonPath, _context);
            pending.Add(_jsonPath, await DbcJson.SerializeAsync(new { fingerprint = _fingerprint,
                count = frames.Count, allValid = _messages.All(m => observed.TryGetValue(m.Identity, out var f) && f.Frame != null && f.Valid),
                frames }, ct).ConfigureAwait(false));
        }
        ct.ThrowIfCancellationRequested();
        return pending;
    }

    // Commit after the caller has successfully published every prepared destination.
    internal void Published(IReadOnlyList<DbcBackgroundFrame> samples)
    {
        foreach (var sample in samples)
            if (sample.Frame != null && (sample.Valid || sample.Quality == "TimeUnknown") && _bindings.ContainsKey(sample.Identity))
                _published[sample.Identity] = sample.Sequence;
    }
}
