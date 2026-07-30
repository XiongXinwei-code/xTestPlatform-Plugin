using MessagePack;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsReadDtcSetting : UdsCommonSetting
{
    /// <summary>DTC 子功能 (常用: 0x01=按状态掩码报告数量, 0x02=按状态掩码报告DTC)</summary>
    public byte SubFunction { get; set; } = 0x02;

    /// <summary>DTC 状态掩码</summary>
    public byte StatusMask { get; set; } = 0xFF;

    /// <summary>结果变量（存储 DTC 列表十六进制数据）</summary>
    public string ResultVariable { get; set; } = "";
}
