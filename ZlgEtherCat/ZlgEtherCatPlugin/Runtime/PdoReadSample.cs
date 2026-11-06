using ZlgEtherCat.Models;

namespace ZlgEtherCat.Runtime;

/// <summary>可选读取结果信封。Sequence 是软件采集序号；时间和年龄从主机读取完成起算，不包含此前 RX 队列等待时间。</summary>
public sealed record PdoReadSample(object Value, long Sequence, ushort SlaveIndex, int Offset, int Length,
    PdoDataType DataType, long ReceivedTimestamp, double LocalAgeMs)
{
    public string TimestampSource => HostReadTimestamp.TimestampSource;
    public long TimestampFrequencyHz => HostReadTimestamp.TimestampFrequencyHz;
    public bool HardwareTimestampAvailable => HostReadTimestamp.HardwareTimestampAvailable;
    public long? HardwareTimestamp => HostReadTimestamp.HardwareTimestamp;
}
