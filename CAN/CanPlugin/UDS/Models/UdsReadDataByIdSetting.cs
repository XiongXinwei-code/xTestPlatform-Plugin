using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsReadDataByIdSetting : UdsCommonSetting
{
    /// <summary>DID（Data Identifier），支持表达式，如 "0xF190"</summary>
    [ExpressionField]
    public string Did { get; set; } = "\"0xF190\"";

    /// <summary>结果变量（存储读取到的十六进制数据）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "";
}
