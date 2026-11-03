using System.Collections.ObjectModel;
using System.Numerics;

namespace CAN.Dbc;

public readonly record struct DbcSignalValue(decimal Value, bool IsRaw = false);
public sealed record DbcDecodedSignal(string Name, decimal RawValue, decimal PhysicalValue, string Unit, string? ValueDescription);

/// <summary>不可变整数信号定义；Motorola 起始位为 MSB，使用 DBC sawtooth 位序。</summary>
public sealed class DbcSignal
{
    public string Name { get; }
    public int StartBit { get; }
    public int BitLength { get; }
    public bool IsLittleEndian { get; }
    public bool IsSigned { get; }
    public decimal Factor { get; }
    public decimal Offset { get; }
    public decimal Minimum { get; }
    public decimal Maximum { get; }
    public string Unit { get; }
    public string Comment { get; }
    public bool IsMultiplexer { get; }
    public decimal? MultiplexerValue { get; }
    /// <summary>GenSigStartValue 的整数原始值；DBC 未提供初值时为 0，不是物理值。</summary>
    public decimal InitialRawValue { get; }
    public bool HasInitialValue { get; }
    public IReadOnlyDictionary<decimal, string> ValueDescriptions { get; }
    private readonly ReadOnlyCollection<int> _positions;

    internal DbcSignal(string name, int start, int length, bool little, bool signed, decimal factor,
        decimal offset, decimal minimum, decimal maximum, string unit, string comment,
        bool multiplexer, decimal? multiplexValue, IReadOnlyDictionary<decimal, string> descriptions,
        decimal? initialRawValue = null)
    {
        Name = name; StartBit = start; BitLength = length; IsLittleEndian = little; IsSigned = signed;
        Factor = factor; Offset = offset; Minimum = minimum; Maximum = maximum; Unit = unit;
        Comment = comment; IsMultiplexer = multiplexer; MultiplexerValue = multiplexValue;
        InitialRawValue = initialRawValue ?? 0; HasInitialValue = initialRawValue.HasValue;
        ValueDescriptions = new ReadOnlyDictionary<decimal, string>(new Dictionary<decimal, string>(descriptions));
        var positions = new int[length]; int bit = start;
        for (int index = 0; index < length; index++)
        { positions[index] = bit; bit = little ? bit + 1 : bit % 8 == 0 ? bit + 15 : bit - 1; }
        _positions = Array.AsReadOnly(positions);
    }

    public IEnumerable<int> BitPositions() => _positions;

    public decimal ToRaw(decimal value, bool isRaw)
    {
        if (!isRaw && (Minimum != 0 || Maximum != 0) && (value < Minimum || value > Maximum))
            throw new ArgumentOutOfRangeException(Name, $"信号 {Name} 物理值 {value} 超出 DBC 范围 [{Minimum}, {Maximum}]");
        BigInteger raw;
        if (isRaw)
        {
            if (value != decimal.Truncate(value)) throw new ArgumentException($"信号 {Name} 原始值必须是整数");
            raw = (BigInteger)value;
        }
        else
        {
            var input = DecimalParts(value); var offset = DecimalParts(Offset); var factor = DecimalParts(Factor);
            int commonScale = Math.Max(input.Scale, offset.Scale);
            BigInteger numerator = input.Coefficient * BigInteger.Pow(10, commonScale - input.Scale)
                - offset.Coefficient * BigInteger.Pow(10, commonScale - offset.Scale);
            BigInteger denominator = factor.Coefficient;
            if (factor.Scale > commonScale) numerator *= BigInteger.Pow(10, factor.Scale - commonScale);
            else denominator *= BigInteger.Pow(10, commonScale - factor.Scale);
            raw = BigInteger.DivRem(numerator, denominator, out var remainder);
            if (remainder != 0) throw new ArgumentException($"信号 {Name} 的值无法精确表示为整数原始值，未执行四舍五入");
        }
        BigInteger minimum = IsSigned ? -(BigInteger.One << (BitLength - 1)) : BigInteger.Zero;
        BigInteger maximum = IsSigned ? (BigInteger.One << (BitLength - 1)) - 1 : (BigInteger.One << BitLength) - 1;
        if (raw < minimum || raw > maximum)
            throw new ArgumentOutOfRangeException(Name, $"信号 {Name} 原始值 {raw} 超出 {BitLength} 位{(IsSigned ? "有符号" : "无符号")}范围");
        return (decimal)raw;
    }

    internal decimal ToPhysical(decimal raw)
    {
        var factor = DecimalParts(Factor); var offset = DecimalParts(Offset);
        int scale = Math.Max(factor.Scale, offset.Scale);
        BigInteger coefficient = (BigInteger)raw * factor.Coefficient * BigInteger.Pow(10, scale - factor.Scale)
            + offset.Coefficient * BigInteger.Pow(10, scale - offset.Scale);
        while (scale > 0 && coefficient % 10 == 0) { coefficient /= 10; scale--; }
        bool negative = coefficient.Sign < 0; coefficient = BigInteger.Abs(coefficient);
        if (coefficient > (BigInteger.One << 96) - 1)
            throw new OverflowException($"信号 {Name} 的物理值不能用 decimal 精确表达，未执行舍入");
        return new decimal((int)(uint)(coefficient & uint.MaxValue), (int)(uint)((coefficient >> 32) & uint.MaxValue),
            (int)(uint)((coefficient >> 64) & uint.MaxValue), negative, (byte)scale);
    }

    private static (BigInteger Coefficient, int Scale) DecimalParts(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
        return (bits[3] < 0 ? -coefficient : coefficient, (bits[3] >> 16) & 0xFF);
    }

    internal decimal Extract(byte[] bytes)
    {
        ulong raw = 0;
        for (int index = 0; index < _positions.Count; index++)
        {
            int bit = _positions[index]; ulong value = (ulong)((bytes[bit / 8] >> (bit % 8)) & 1);
            if (IsLittleEndian) raw |= value << index; else raw = (raw << 1) | value;
        }
        return IsSigned && (raw & (1UL << (BitLength - 1))) != 0 ? (decimal)raw - Pow2(BitLength) : raw;
    }

    internal void Insert(byte[] bytes, decimal raw)
    {
        ulong bits = (ulong)(raw < 0 ? raw + Pow2(BitLength) : raw);
        for (int index = 0; index < _positions.Count; index++)
        {
            int bit = _positions[index]; int shift = IsLittleEndian ? index : BitLength - index - 1;
            if (((bits >> shift) & 1) != 0) bytes[bit / 8] |= (byte)(1 << (bit % 8));
        }
    }

    internal static decimal Pow2(int exponent)
    {
        decimal result = 1;
        for (int i = 0; i < exponent; i++) result *= 2;
        return result;
    }
}

public sealed class DbcMessage
{
    public uint Id { get; }
    public bool Extended { get; }
    public string Name { get; }
    public int Length { get; }
    public bool IsFd { get; }
    public int? CycleTimeMs { get; }
    public string CycleTimeSource { get; }
    public IReadOnlyList<DbcSignal> Signals { get; }
    public DbcSignal? MultiplexerSignal { get; }
    private readonly IReadOnlyDictionary<string, DbcSignal> _byName;

    internal DbcMessage(uint id, bool extended, string name, int length, bool isFd, IEnumerable<DbcSignal> signals,
        int? cycleTimeMs = null, string cycleTimeSource = "未声明有效周期")
    {
        Id = id; Extended = extended; Name = name; Length = length; IsFd = isFd;
        CycleTimeMs = cycleTimeMs; CycleTimeSource = cycleTimeSource;
        Signals = Array.AsReadOnly(signals.ToArray());
        MultiplexerSignal = Signals.SingleOrDefault(s => s.IsMultiplexer);
        _byName = new ReadOnlyDictionary<string, DbcSignal>(Signals.ToDictionary(s => s.Name, StringComparer.Ordinal));
    }

    public IReadOnlyList<DbcSignal> GetActiveSignals(decimal? multiplexerRaw)
    {
        if (MultiplexerSignal == null) return Signals;
        if (multiplexerRaw == null) throw new ArgumentException($"报文 {Name} 需要复用选择信号 {MultiplexerSignal.Name}");
        MultiplexerSignal.ToRaw(multiplexerRaw.Value, true);
        return Array.AsReadOnly(Signals.Where(s => s.MultiplexerValue == null || s.MultiplexerValue == multiplexerRaw).ToArray());
    }

    public byte[] Encode(IReadOnlyDictionary<string, DbcSignalValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (string name in values.Keys)
            if (!_byName.ContainsKey(name)) throw new ArgumentException($"报文 {Name} 没有信号 {name}");
        decimal? muxRaw = null;
        if (MultiplexerSignal is { } selector)
        {
            if (!values.TryGetValue(selector.Name, out var value)) throw new ArgumentException($"缺少复用选择信号 {selector.Name}");
            muxRaw = selector.ToRaw(value.Value, value.IsRaw);
        }
        var bytes = new byte[Length];
        foreach (var signal in GetActiveSignals(muxRaw))
        {
            if (!values.TryGetValue(signal.Name, out var value)) throw new ArgumentException($"缺少活动信号 {signal.Name}");
            signal.Insert(bytes, signal.ToRaw(value.Value, value.IsRaw));
        }
        return bytes;
    }

    public IReadOnlyList<DbcDecodedSignal> Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < Length) throw new ArgumentException($"报文 {Name} 实际 {data.Length} 字节，小于 DBC 定义 {Length} 字节");
        if (data.Length > 64) throw new ArgumentException("CAN 报文载荷超过 64 字节");
        decimal? muxRaw = MultiplexerSignal?.Extract(data);
        var result = new List<DbcDecodedSignal>();
        foreach (var signal in GetActiveSignals(muxRaw))
        {
            decimal raw = signal.Extract(data);
            result.Add(new(signal.Name, raw, signal.ToPhysical(raw), signal.Unit,
                signal.ValueDescriptions.TryGetValue(raw, out var description) ? description : null));
        }
        return result.AsReadOnly();
    }
}
