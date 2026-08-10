using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.XCP.Models;

/// <summary>XCP_ShortUpload 步骤设置（从 ECU 读取内存）</summary>
[MessagePackObject(true)]
public class XcpShortUploadSetting : XcpCommonSetting
{
    /// <summary>ECU 内存地址（支持表达式，如 "0x40001000"）</summary>
    [ExpressionField]
    public string Address { get; set; } = "\"0x40001000\"";

    /// <summary>地址扩展</summary>
    public XcpAddressExtension AddressExtension { get; set; } = XcpAddressExtension.None;

    /// <summary>读取字节数（1-7）</summary>
    public int ReadLength { get; set; } = 4;

    /// <summary>字节序</summary>
    public XcpByteOrder ByteOrder { get; set; } = XcpByteOrder.LittleEndian;

    /// <summary>结果存储变量路径</summary>
    public string ResultVariable { get; set; } = string.Empty;
}
