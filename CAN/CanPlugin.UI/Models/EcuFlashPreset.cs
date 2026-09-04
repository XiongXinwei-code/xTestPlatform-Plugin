using CAN.Flash.Models;

namespace CAN.UI.Models;

/// <summary>
/// ECU 刷写规范预设。保存与 ECU 厂商刷写规范相关的参数，
/// 不包含固件路径与 CAN 连接信息（这些随每个测试序列变化）。
/// </summary>
public sealed class EcuFlashPreset
{
    /// <summary>预设名称，作为唯一标识</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>备注说明</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>地址与长度格式标识表达式</summary>
    public string AddressAndLengthFormatId { get; set; } = "\"0x44\"";

    /// <summary>数据格式标识表达式</summary>
    public string DataFormatId { get; set; } = "\"0x00\"";

    /// <summary>是否在下载前执行擦除例程</summary>
    public bool EraseBeforeDownload { get; set; } = true;

    /// <summary>擦除例程 ID 表达式</summary>
    public string EraseRoutineId { get; set; } = "\"0xFF00\"";

    /// <summary>擦除例程是否携带地址和长度参数</summary>
    public bool EraseWithAddressAndLength { get; set; } = true;

    /// <summary>擦除超时（ms）</summary>
    public int EraseTimeoutMs { get; set; } = 30000;

    /// <summary>单块最大传输字节数；0 表示采用 ECU 返回值</summary>
    public int MaxBlockSize { get; set; } = 512;

    /// <summary>下载前等待时间（ms）</summary>
    public int PreDownloadDelayMs { get; set; }

    /// <summary>块传输失败后的重试次数</summary>
    public int BlockRetryCount { get; set; } = 2;

    /// <summary>每块传输之间的间隔（ms）</summary>
    public int InterBlockDelayMs { get; set; }

    /// <summary>校验方式</summary>
    public FlashCheckMode CheckMode { get; set; } = FlashCheckMode.Crc32;

    /// <summary>校验例程 ID 表达式</summary>
    public string CheckRoutineId { get; set; } = "\"0x0202\"";

    public override string ToString() => Name;
}
