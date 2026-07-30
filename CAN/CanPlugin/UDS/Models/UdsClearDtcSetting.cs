using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsClearDtcSetting : UdsCommonSetting
{
    /// <summary>DTC 组（3 字节，0xFFFFFF=全部清除）</summary>
    [ExpressionField]
    public string DtcGroup { get; set; } = "0xFFFFFF";
}
