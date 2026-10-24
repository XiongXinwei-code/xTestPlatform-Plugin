using CAN.CANopen;
using CiA402.Core;
using CiA402.Models;

namespace CiA402.Transport;

public sealed class CanopenAxisTransport : ICia402AxisTransport, IAxisTransportDiagnostics
{
    public bool SupportsConcurrentMonitoring => true;
    public bool FeedbackIsPassiveReceive => _feedback.Count != 0;
    private sealed record Pdo(uint CobId, IReadOnlyList<PdoEntry> Entries);
    private readonly CanopenNodeAccess _node;
    private readonly BindAxisSetting _config;
    private readonly Dictionary<OperationMode, Pdo> _targets = new();
    private readonly List<Pdo> _feedback = new();
    private readonly Dictionary<uint, long> _lastFeedbackTimestamps = new();
    private string _diagnostic = "CANopen 尚未开始 I/O";
    public string DiagnosticContext => Volatile.Read(ref _diagnostic);

    public CanopenAxisTransport(CanopenNodeAccess node, BindAxisSetting configuration) => (_node, _config) = (node, configuration);
    private ushort Index(int value) => checked((ushort)(value + _config.ObjectIndexOffset));
    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
    {
        Volatile.Write(ref _diagnostic, $"CANopen SDO 读对象 0x{index:X4}:{subIndex:X2}");
        return _node.ReadObjectAsync(index, subIndex, ct);
    }
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
    {
        Volatile.Write(ref _diagnostic, $"CANopen SDO 写对象 0x{index:X4}:{subIndex:X2}，长度={value.Length}");
        return _node.WriteObjectAsync(index, subIndex, value, ct);
    }
    private async Task<long> Read(ushort index, byte sub, int bytes, bool signed, CancellationToken ct)
        => ObjectCodec.Decode(await ReadObjectAsync(index, sub, ct).ConfigureAwait(false), bytes, signed);

    public async Task InitializeAsync(CancellationToken ct)
    {
        var errors = PdoConfiguration.Validate(_config);
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors));
        await AddTarget(OperationMode.CSP, _config.CspPdoNumber, _config.CspPdoLayout, 0x607A, 32, ct).ConfigureAwait(false);
        await AddTarget(OperationMode.CSV, _config.CsvPdoNumber, _config.CsvPdoLayout, 0x60FF, 32, ct).ConfigureAwait(false);
        await AddTarget(OperationMode.CST, _config.CstPdoNumber, _config.CstPdoLayout, 0x6071, 16, ct).ConfigureAwait(false);
        foreach (var mapping in PdoConfiguration.FeedbackMappings(_config))
        {
            var pdo = await VerifyPdo(mapping.Number, PdoLayout.Parse(mapping.Layout), true, ct).ConfigureAwait(false);
            if (_feedback.Any(p => p.CobId == pdo.CobId)) throw new InvalidOperationException($"多个反馈 TPDO 使用同一个 COB-ID 0x{pdo.CobId:X}");
            _feedback.Add(pdo);
        }
        var ids = _targets.Values.Select(x => x.CobId).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("不同运行模式的 RPDO 不得使用同一个 COB-ID");
        if (ids.Intersect(_feedback.Select(p => p.CobId)).Any())
            throw new InvalidOperationException("目标 RPDO 与反馈 TPDO 不能使用同一个 COB-ID，请核对设备通信参数");
    }

    private async Task AddTarget(OperationMode mode, int number, string layout, int target, int bits, CancellationToken ct)
    {
        if (number == 0) return;
        var entries = PdoLayout.Parse(layout);
        if (entries.Count(e => e.Index == Index(target) && e.SubIndex == 0 && e.Bits == bits) != 1 ||
            entries.Any(e => !(e.Index == Index(target) && e.SubIndex == 0 && e.Bits == bits) &&
                             !(e.Index == Index(0x6040) && e.SubIndex == 0 && e.Bits == 16)))
            throw new ArgumentException($"{mode} RPDO 必须包含当前轴目标对象，只可附加当前轴控制字；索引须显式包含轴偏移");
        _targets.Add(mode, await VerifyPdo(number, entries, false, ct).ConfigureAwait(false));
    }

    private async Task<Pdo> VerifyPdo(int number, IReadOnlyList<PdoEntry> entries, bool transmit, CancellationToken ct)
    {
        var communication = checked((ushort)((transmit ? 0x1800 : 0x1400) + number - 1));
        var mapping = checked((ushort)((transmit ? 0x1A00 : 0x1600) + number - 1));
        var cobId = (uint)await Read(communication, 1, 4, false, ct).ConfigureAwait(false);
        if ((cobId & 0xA0000000) != 0 || (cobId & 0x1FFFFFFF) > 0x7FF || (cobId & 0x7FF) == 0)
            throw new InvalidOperationException($"PDO {number} 未启用或不是有效标准帧 COB-ID");
        var type = await Read(communication, 2, 1, false, ct).ConfigureAwait(false);
        if (type is > 240 and < 254) throw new InvalidOperationException($"PDO {number} 的传输类型 {type} 不受支持");
        var count = await Read(mapping, 0, 1, false, ct).ConfigureAwait(false);
        if (count != entries.Count) throw new InvalidOperationException($"PDO {number} 映射数量与配置不一致，请在绑定前配置设备");
        for (var i = 0; i < entries.Count; i++)
            if ((uint)await Read(mapping, (byte)(i + 1), 4, false, ct).ConfigureAwait(false) != entries[i].Descriptor)
                throw new InvalidOperationException($"PDO {number} 第 {i + 1} 项映射与配置不一致");
        return new Pdo(cobId & 0x7FF, entries);
    }

    public void RequireCyclicMode(OperationMode mode)
    {
        if (!_targets.ContainsKey(mode)) throw new InvalidOperationException($"未配置并验证 {mode} RPDO，不能使用该周期模式");
    }

    public async Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct)
    {
        RequireCyclicMode(mode);
        var pdo = _targets[mode];
        Volatile.Write(ref _diagnostic, $"CANopen RPDO 0x{pdo.CobId:X}，模式={mode}");
        var data = pdo.Entries.SelectMany(e => e.Index == Index(0x6040)
            ? ObjectCodec.Encode(controlWord, 2, false)
            : ObjectCodec.Encode(target, e.Bits / 8, true)).ToArray();
        await _node.SendPdoAsync(pdo.CobId, data, ct).ConfigureAwait(false);
    }

    public async Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
    {
        if (_feedback.Count == 0)
        {
            var status = (ushort)await Read(Index(0x6041), 0, 2, false, ct).ConfigureAwait(false);
            int? p = position ? (int)await Read(Index(0x6064), 0, 4, true, ct).ConfigureAwait(false) : null;
            int? v = velocity ? (int)await Read(Index(0x606C), 0, 4, true, ct).ConfigureAwait(false) : null;
            short? t = torque ? (short)await Read(Index(0x6077), 0, 2, true, ct).ConfigureAwait(false) : null;
            return new(status, p, v, t);
        }
        // 一个对象如在多个 TPDO 中重复映射，使用配置顺序中的第一个来源，不让后到帧覆盖。
        var sources = new Dictionary<ushort, Pdo>();
        foreach (var (enabled, index) in new[] { (true, 0x6041), (position, 0x6064), (velocity, 0x606C), (torque, 0x6077) })
        {
            if (!enabled) continue;
            var source = _feedback.FirstOrDefault(p => p.Entries.Any(e => e.Index == Index(index) && e.SubIndex == 0))
                ?? throw new InvalidOperationException($"反馈 TPDO 未映射输出所需对象 0x{Index(index):X4}");
            sources.Add(Index(index), source);
        }
        var required = sources.Values.Distinct().ToArray();
        var frames = new Dictionary<uint, CanopenReceivedFrame>();
        var expired = 0;
        var duplicate = 0;
        var unknownTime = 0;
        var receivedCount = 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_config.CommunicationTimeoutMs);
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var missing = required.FirstOrDefault(p => !frames.TryGetValue(p.CobId, out var f) || f.QueueAge.TotalMilliseconds > _config.FeedbackMaxAgeMs);
                Snapshot(missing?.CobId);
                if (missing == null)
                {
                    // 同一卡的硬件时间戳只比较跨度，不与主机时钟混用；仍不保证同一 SYNC 周期。
                    var stamped = frames.Where(p => p.Value.Frame.TimestampNs > 0).ToArray();
                    if (stamped.Length > 1 && stamped.Max(p => p.Value.Frame.TimestampNs) - stamped.Min(p => p.Value.Frame.TimestampNs) > (long)_config.FeedbackMaxAgeMs * 1_000_000)
                    {
                        missing = required.Single(p => p.CobId == stamped.MinBy(p => p.Value.Frame.TimestampNs).Key);
                        Snapshot(missing.CobId);
                    }
                    else return DecodeFeedback(sources, frames, position, velocity, torque);
                }
                var received = await _node.ReceivePdoAsync(missing.CobId, deadline.Token).ConfigureAwait(false);
                if (received == null) throw FeedbackTimeout(required, frames);
                var frame = received.Frame;
                receivedCount++;
                if (!received.HasKnownObservationTime) { unknownTime++; Snapshot(missing.CobId); continue; }
                if (received.QueueAge.TotalMilliseconds > _config.FeedbackMaxAgeMs) { expired++; Snapshot(missing.CobId); continue; }
                if (frame.TimestampNs > 0 && _lastFeedbackTimestamps.TryGetValue(missing.CobId, out var last) && frame.TimestampNs <= last) { duplicate++; Snapshot(missing.CobId); continue; }
                if (frame.Data.Length != missing.Entries.Sum(e => e.Bits / 8))
                    throw new InvalidOperationException($"反馈 TPDO 0x{missing.CobId:X} 数据长度与映射不一致：实际 {frame.Data.Length} 字节");
                if (frame.TimestampNs > 0) _lastFeedbackTimestamps[missing.CobId] = frame.TimestampNs;
                frames[missing.CobId] = received;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        { throw FeedbackTimeout(required, frames); }

        void Snapshot(uint? waiting)
        {
            var missingIds = required.Where(p => !frames.TryGetValue(p.CobId, out var f) || f.QueueAge.TotalMilliseconds > _config.FeedbackMaxAgeMs);
            Volatile.Write(ref _diagnostic, $"CANopen TPDO 所需=[{string.Join(",", required.Select(p => $"0x{p.CobId:X}"))}]; 已收=[{string.Join(",", frames.Keys.Select(id => $"0x{id:X}"))}]; 缺失/过期=[{string.Join(",", missingIds.Select(p => $"0x{p.CobId:X}"))}]; 正在等待={(waiting is { } id ? $"0x{id:X}" : "无")}; 收到={receivedCount}; 丢弃过期={expired}; 丢弃重复时间戳={duplicate}; 时间来源未知={unknownTime}; FeedbackMaxAgeMs={_config.FeedbackMaxAgeMs}");
        }
    }

    private AxisFeedback DecodeFeedback(Dictionary<ushort, Pdo> sources, Dictionary<uint, CanopenReceivedFrame> frames, bool position, bool velocity, bool torque)
    {
        long Value(int index)
        {
            var source = sources[Index(index)];
            var offset = 0;
            foreach (var entry in source.Entries)
            {
                if (entry.Index == Index(index) && entry.SubIndex == 0)
                    return ObjectCodec.Decode(frames[source.CobId].Frame.Data[offset..(offset + entry.Bits / 8)], entry.Bits / 8, index != 0x6041);
                offset += entry.Bits / 8;
            }
            throw new InvalidOperationException($"反馈对象 0x{Index(index):X4} 未找到来源");
        }
        return new((ushort)Value(0x6041), position ? (int)Value(0x6064) : null,
            velocity ? (int)Value(0x606C) : null, torque ? (short)Value(0x6077) : null);
    }

    private TimeoutException FeedbackTimeout(IEnumerable<Pdo> required, Dictionary<uint, CanopenReceivedFrame> frames)
    {
        var missing = required.Where(p => !frames.TryGetValue(p.CobId, out var frame) || frame.QueueAge.TotalMilliseconds > _config.FeedbackMaxAgeMs)
            .Select(p => $"0x{p.CobId:X}");
        return new($"在 {_config.CommunicationTimeoutMs} ms 内未收齐有效且未重复的反馈 TPDO，所需 ID=[{string.Join(",", required.Select(p => $"0x{p.CobId:X}"))}]，缺失/过期 ID=[{string.Join(",", missing)}]；请检查发送触发、SYNC 和时间戳合并窗口");
    }

    public void Dispose() => _node.Dispose();
}
