namespace CAN.Flash.HexParser;

/// <summary>
/// 固件中一段地址连续的数据块。
/// </summary>
public sealed class FlashSegment
{
    /// <summary>段起始地址</summary>
    public uint StartAddress { get; set; }

    /// <summary>段数据（地址连续）</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>段长度</summary>
    public int Length => Data.Length;

    /// <summary>段结束地址（不含）</summary>
    public uint EndAddress => StartAddress + (uint)Data.Length;

    public override string ToString() =>
        $"0x{StartAddress:X8}-0x{EndAddress:X8} ({Data.Length} 字节)";
}
