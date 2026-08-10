namespace LIN.Models;

/// <summary>统一 LIN 帧结构</summary>
public class LinFrame
{
    /// <summary>LIN 帧 ID（0-63，不含奇偶校验位）</summary>
    public byte FrameId { get; set; }

    /// <summary>数据载荷（1-8 字节）</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>校验类型</summary>
    public LinChecksumType ChecksumType { get; set; } = LinChecksumType.Enhanced;

    /// <summary>接收时间戳（纳秒）</summary>
    public long TimestampNs { get; set; }

    /// <summary>数据长度</summary>
    public int DataLength => Data.Length;
}
