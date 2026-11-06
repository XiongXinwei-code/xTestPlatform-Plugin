using System.Diagnostics;

namespace ZlgEtherCat.Runtime;

/// <summary>主机读取完成时的单调时钟元信息；不表示设备采样时刻或读取前的队列等待时间。</summary>
public static class HostReadTimestamp
{
    public const string TimestampSource = "HostMonotonicReadCompletion";
    public static long TimestampFrequencyHz => Stopwatch.Frequency;
    public const bool HardwareTimestampAvailable = false;
    public static long? HardwareTimestamp => null;
}
