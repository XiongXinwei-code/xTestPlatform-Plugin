using System.Text.Json.Serialization;

namespace CAN.Flash.Models;

/// <summary>固件文件格式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FirmwareFormat
{
    /// <summary>按文件扩展名自动识别</summary>
    Auto = 0,
    /// <summary>Intel HEX (.hex)</summary>
    IntelHex = 1,
    /// <summary>Motorola S-Record (.s19/.srec/.mot)</summary>
    SRecord = 2,
    /// <summary>原始二进制 (.bin)</summary>
    Binary = 3
}

/// <summary>烧录完成后的校验方式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlashCheckMode
{
    /// <summary>不执行校验</summary>
    None = 0,
    /// <summary>调用校验例程并附带 CRC32 值</summary>
    Crc32 = 1,
    /// <summary>调用校验例程并附带字节累加和（4 字节）</summary>
    Checksum = 2
}
