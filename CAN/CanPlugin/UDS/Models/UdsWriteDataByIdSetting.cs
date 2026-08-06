using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsWriteDataByIdSetting : UdsCommonSetting
{
    /// <summary>DID（Data Identifier）</summary>
    [ExpressionField]
    public string Did { get; set; } = "\"0xF190\"";

    /// <summary>写入数据（十六进制字符串）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"\"";
}
