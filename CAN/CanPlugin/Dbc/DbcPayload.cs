using System.Globalization;
using CAN.Helpers;
using CAN.Models;

namespace CAN.Dbc;

/// <summary>DBC 信号编码与用户显式 RC/CRC 字节覆盖之间的唯一契约。</summary>
public static class DbcPayload
{
    public static void ValidateReadProtection(CanPayloadProtectionSetting? protection, int length)
    {
        // Receiving extracts RC; generation rules belong exclusively to transmitters.
        var copy = protection?.Copy();
        if (copy != null)
        {
            copy.RcInitialValue = 0; copy.RcMinimum = 0; copy.RcMaximum = 0; copy.RcStep = 0;
            copy.RcOverflow = CanCounterOverflow.Wrap;
        }
        CanPayloadProtection.Validate(copy, length);
    }

    public static bool ResolveFd(DbcMessage message, DbcFrameFormat format)
    {
        bool fd = format switch
        {
            DbcFrameFormat.Auto => message.IsFd,
            DbcFrameFormat.Classic => false,
            DbcFrameFormat.Fd => true,
            _ => throw new ArgumentException("DBC 帧格式选择无效")
        };
        if (!fd && message.Length > 8) throw new ArgumentException("DBC 报文超过 8 字节，不能按经典 CAN 发送或读取");
        return fd;
    }

    public static IReadOnlySet<string> ValidateProtection(DbcMessage message, CanPayloadProtectionSetting? protection)
    {
        CanPayloadProtection.Validate(protection, message.Length);
        var automatic = new HashSet<string>(StringComparer.Ordinal);
        if (protection == null) return automatic;
        bool ControlledBit(int bit) => CanPayloadProtection.ControlsBit(protection, bit);
        foreach (var signal in message.Signals)
        {
            var bits = signal.BitPositions().ToArray();
            if (!bits.Any(ControlledBit)) continue;
            if (signal.IsMultiplexer) throw new ArgumentException($"RC/CRC/Data ID 自动字段不能覆盖复用选择信号 {signal.Name}");
            if (!bits.All(ControlledBit)) throw new ArgumentException($"信号 {signal.Name} 跨越 RC/CRC/Data ID 覆盖边界，请调整字段范围，不能部分覆盖信号");
            automatic.Add(signal.Name);
        }
        return automatic;
    }

    internal static object DecodeFrame(DbcMessage message, CanMessage frame, CanPayloadProtectionSetting? protection,
        IReadOnlyList<DbcDecodedSignal>? decoded = null)
    {
        var signals = decoded ?? message.Decode(frame.Data);
        var integrity = InspectProtection(frame.Data.AsSpan(0, message.Length).ToArray(), protection);
        return new
        {
            id = $"0x{frame.Id:X}", name = message.Name, frameType = frame.FrameType.ToString(), isFd = frame.IsFd,
            definedLength = message.Length, receivedLength = frame.Data.Length, data = Convert.ToHexString(frame.Data),
            frame.TimestampNs, frame.RawTimestamp100ns, frame.RawTimestampUs, frame.TimestampDomain, frame.ClockId,
            frame.IngressMonotonicTicks, frame.CaptureMonotonicLowerBoundTicks,
            integrity,
            signals = signals.Select(signal => new
            {
                name = signal.Name, rawValue = signal.RawValue.ToString(CultureInfo.InvariantCulture),
                physicalValue = signal.PhysicalValue, unit = signal.Unit, valueDescription = signal.ValueDescription
            }).ToArray()
        };
    }

    internal static DbcIntegrity InspectProtection(byte[] payload, CanPayloadProtectionSetting? protection)
    {
        ValidateReadProtection(protection, payload.Length);
        if (protection == null) return new(null, null, null, null);
        ulong Read(int start, int end, CanPayloadByteOrder order)
        {
            ulong value = 0;
            for (int i = 0; i <= end - start; i++)
                value |= (ulong)payload[start + i] << (8 * (order == CanPayloadByteOrder.LittleEndian ? i : end - start - i));
            return value;
        }
        string? rc = protection.RcEnabled ? (protection.RcUseBitField
            ? (ulong)((payload[protection.RcStartByte] >> protection.RcBitOffset) & ((1 << protection.RcBitLength) - 1))
            : Read(protection.RcStartByte, protection.RcEndByte, protection.RcByteOrder)).ToString(CultureInfo.InvariantCulture) : null;
        if (!protection.CrcEnabled) return new(rc, null, null, null);
        var snapshot = CanPayloadProtection.Snapshot.Capture(protection);
        ulong expected = snapshot.CalculateCrc(payload);
        ulong actual = Read(protection.CrcStartByte, protection.CrcEndByte, protection.CrcByteOrder);
        return new(rc, $"0x{actual:X}", $"0x{expected:X}", actual == expected,
            snapshot.Profile?.DataIdValid(payload), snapshot.Profile?.CounterValid(payload));
    }
}

internal sealed record DbcIntegrity(string? RcValue, string? CrcActual, string? CrcExpected, bool? CrcValid,
    bool? DataIdValid = null, bool? CounterValid = null)
{
    public bool? ProtectionValid => CrcValid == null ? null : CrcValid == true && DataIdValid != false && CounterValid != false;
}
