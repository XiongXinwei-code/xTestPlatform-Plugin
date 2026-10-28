using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsSecurityAccessSetting : UdsCommonSetting
{
    /// <summary>安全等级序号 1~63；Seed 子功能=2*等级-1，Key=2*等级，保留原有线协议映射。</summary>
    public int SecurityLevel { get; set; } = 1;

    /// <summary>存储 ECU 返回 Seed 的变量名（供 KeyExpression 引用）</summary>
    [VariablePathField]
    public string SeedVariable { get; set; } = "";

    /// <summary>
    /// Key 计算表达式。运行时变量名由 SeedVariable 决定，类型为 byte[]。
    /// 表达式应返回 byte[]。
    /// 示例: "new byte[] { (byte)(Seed[0] ^ 0xA5), (byte)(Seed[1] ^ 0x5A) }"
    /// </summary>
    [ExpressionField]
    public string KeyExpression { get; set; } = "";

    /// <summary>结果变量（存储解锁状态 true/false）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "";
}
