using System.Diagnostics;
using CAN.CANopen;
using CiA402.Core;
using CiA402.Models;

namespace CiA402.Transport;

public sealed class CanopenAxisTransport : ICia402AxisTransport
{
    public bool SupportsConcurrentMonitoring => true;
    private sealed record Pdo(uint CobId, IReadOnlyList<PdoEntry> Entries);
    private readonly CanopenNodeAccess _node;
    private readonly BindAxisSetting _config;
    private readonly Dictionary<OperationMode, Pdo> _targets = new();
    private Pdo? _feedback;
    private long _lastFeedbackTimestamp;

    public CanopenAxisTransport(CanopenNodeAccess node, BindAxisSetting configuration) => (_node, _config) = (node, configuration);
    private ushort Index(int value) => checked((ushort)(value + _config.ObjectIndexOffset));
    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct) => _node.ReadObjectAsync(index, subIndex, ct);
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct) => _node.WriteObjectAsync(index, subIndex, value, ct);
    private async Task<long> Read(ushort index, byte sub, int bytes, bool signed, CancellationToken ct)
        => ObjectCodec.Decode(await ReadObjectAsync(index, sub, ct).ConfigureAwait(false), bytes, signed);

    public async Task InitializeAsync(CancellationToken ct)
    {
        await AddTarget(OperationMode.CSP, _config.CspPdoNumber, _config.CspPdoLayout, 0x607A, 32, ct).ConfigureAwait(false);
        await AddTarget(OperationMode.CSV, _config.CsvPdoNumber, _config.CsvPdoLayout, 0x60FF, 32, ct).ConfigureAwait(false);
        await AddTarget(OperationMode.CST, _config.CstPdoNumber, _config.CstPdoLayout, 0x6071, 16, ct).ConfigureAwait(false);
        if (_config.FeedbackPdoNumber != 0)
        {
            var entries = PdoLayout.Parse(_config.FeedbackPdoLayout);
            var allowed = new Dictionary<ushort, int> { [Index(0x6041)] = 16, [Index(0x6064)] = 32, [Index(0x606C)] = 32, [Index(0x6077)] = 16 };
            if (!entries.Any(e => e.Index == Index(0x6041)) || entries.Any(e => e.SubIndex != 0 || !allowed.TryGetValue(e.Index, out var bits) || bits != e.Bits))
                throw new ArgumentException("反馈 TPDO 必须含状态字，且只能映射当前轴的状态、位置、速度或转矩对象");
            _feedback = await VerifyPdo(_config.FeedbackPdoNumber, entries, true, ct).ConfigureAwait(false);
        }
        var ids = _targets.Values.Select(x => x.CobId).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("不同运行模式的 RPDO 不得使用同一个 COB-ID");
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
        var data = pdo.Entries.SelectMany(e => e.Index == Index(0x6040)
            ? ObjectCodec.Encode(controlWord, 2, false)
            : ObjectCodec.Encode(target, e.Bits / 8, true)).ToArray();
        await _node.SendPdoAsync(pdo.CobId, data, ct).ConfigureAwait(false);
    }

    public async Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
    {
        if (_feedback == null)
        {
            var status = (ushort)await Read(Index(0x6041), 0, 2, false, ct).ConfigureAwait(false);
            int? p = position ? (int)await Read(Index(0x6064), 0, 4, true, ct).ConfigureAwait(false) : null;
            int? v = velocity ? (int)await Read(Index(0x606C), 0, 4, true, ct).ConfigureAwait(false) : null;
            short? t = torque ? (short)await Read(Index(0x6077), 0, 2, true, ct).ConfigureAwait(false) : null;
            return new(status, p, v, t);
        }
        var required = new[] { (position, 0x6064), (velocity, 0x606C), (torque, 0x6077) };
        foreach (var (enabled, index) in required)
            if (enabled && !_feedback.Entries.Any(e => e.Index == Index(index)))
                throw new InvalidOperationException($"反馈 TPDO 未映射输出所需对象 0x{Index(index):X4}");
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < _config.CommunicationTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            var received = await _node.ReceivePdoAsync(_feedback.CobId, ct).ConfigureAwait(false);
            if (received == null) break;
            if (received.QueueAge.TotalMilliseconds > _config.FeedbackMaxAgeMs) continue;
            var frame = received.Frame;
            if (frame.TimestampNs > 0 && frame.TimestampNs <= _lastFeedbackTimestamp) continue;
            if (frame.Data.Length != _feedback.Entries.Sum(e => e.Bits / 8)) throw new InvalidOperationException("反馈 TPDO 数据长度与配置不一致");
            _lastFeedbackTimestamp = frame.TimestampNs;
            var values = new Dictionary<ushort, long>();
            var offset = 0;
            foreach (var e in _feedback.Entries)
            {
                values[e.Index] = ObjectCodec.Decode(frame.Data[offset..(offset + e.Bits / 8)], e.Bits / 8, e.Index != Index(0x6041));
                offset += e.Bits / 8;
            }
            return new((ushort)values[Index(0x6041)], position ? (int)values[Index(0x6064)] : null,
                velocity ? (int)values[Index(0x606C)] : null, torque ? (short)values[Index(0x6077)] : null);
        }
        throw new TimeoutException($"在 {_config.CommunicationTimeoutMs} ms 内未收到有效且未重复的反馈 TPDO");
    }

    public void Dispose() => _node.Dispose();
}
