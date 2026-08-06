using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.XCP.Models;

/// <summary>XCP_ShortDownload 步骤设置（向 ECU 写入内存）</summary>
[MessagePackObject(true)]
public class XcpShortDownloadSetting : XcpCommonSetting
{
    /// <summary>ECU 内存地址（支持表达式）</summary>
    [ExpressionField]
    public string Address { get; set; } = "\"0x40001000\"";

    /// <summary>地址扩展</summary>
    public XcpAddressExtension AddressExtension { get; set; } = XcpAddressExtension.None;

    /// <summary>要写入的数据（十六进制字符串，如 "01 02 03 04"，支持表达式）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"01 00 00 00\"";

    /// <summary>字节序</summary>
    public XcpByteOrder ByteOrder { get; set; } = XcpByteOrder.LittleEndian;
}
