namespace CAN.Models;

/// <summary>统一 CAN 报文结构，支持 Classic/FD</summary>
public class CanMessage
{
    /// <summary>CAN ID（标准帧 11-bit，扩展帧 29-bit）</summary>
    public uint Id { get; set; }

    /// <summary>帧类型（标准/扩展）</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>数据载荷</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>是否为 CAN FD 帧（BRS 位速率切换）</summary>
    public bool IsFd { get; set; }

    /// <summary>接收时间戳（纳秒）</summary>
    public long TimestampNs { get; set; }

    /// <summary>是否为 NI-XNET 在发送成功后生成的发送回显。</summary>
    public bool IsTransmitEcho { get; set; }

    /// <summary>数据长度码 DLC</summary>
    public int Dlc => Data.Length;
}
