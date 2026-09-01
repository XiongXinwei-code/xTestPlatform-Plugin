using CAN.UDS.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Flash.Models;

/// <summary>
/// UDS 固件烧录设置。本插件不切换诊断会话、不执行安全访问，
/// 需由前置步骤完成 0x10 会话切换与 0x27 解锁。
/// </summary>
[MessagePackObject(true)]
public class CanFlashSetting : UdsCommonSetting
{
    /// <summary>固件文件路径</summary>
    [ExpressionField]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>固件文件格式</summary>
    public FirmwareFormat Format { get; set; } = FirmwareFormat.Auto;

    /// <summary>二进制格式的基地址（仅 Binary 格式使用）</summary>
    [ExpressionField]
    public string BaseAddress { get; set; } = "\"0x08000000\"";

    /// <summary>
    /// 地址与长度格式标识符（0x44 表示 4 字节地址 + 4 字节长度）。
    /// 用于擦除例程的 option record 与 0x34 RequestDownload。
    /// </summary>
    [ExpressionField]
    public string AddressAndLengthFormatId { get; set; } = "\"0x44\"";

    /// <summary>数据格式标识符（0x00 表示不压缩不加密）</summary>
    [ExpressionField]
    public string DataFormatId { get; set; } = "\"0x00\"";

    /// <summary>是否在下载前执行擦除例程</summary>
    public bool EraseBeforeDownload { get; set; } = true;

    /// <summary>擦除例程 ID</summary>
    [ExpressionField]
    public string EraseRoutineId { get; set; } = "\"0xFF00\"";

    /// <summary>擦除操作超时（ms），擦除耗时通常远长于普通请求</summary>
    public int EraseTimeoutMs { get; set; } = 30000;

    /// <summary>单块最大传输字节数，实际取值不超过 ECU 在 0x34 响应中允许的长度</summary>
    public int MaxBlockSize { get; set; } = 512;

    /// <summary>烧录完成后的校验方式</summary>
    public FlashCheckMode CheckMode { get; set; } = FlashCheckMode.Crc32;

    /// <summary>校验例程 ID（CheckMode 非 None 时使用）</summary>
    [ExpressionField]
    public string CheckRoutineId { get; set; } = "\"0x0202\"";

    /// <summary>单块传输失败后的重试次数</summary>
    public int BlockRetryCount { get; set; } = 2;

    /// <summary>每块传输之间的间隔（ms），用于适配处理较慢的 Bootloader</summary>
    public int InterBlockDelayMs { get; set; } = 0;

    /// <summary>进度变量名（写入 0~100 的整数百分比），为空则不写入</summary>
    [VariablePathField]
    public string ProgressVariable { get; set; } = string.Empty;

    /// <summary>结果变量名（写入实际烧录的总字节数），为空则不写入</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
