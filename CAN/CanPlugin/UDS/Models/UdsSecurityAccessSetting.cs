using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsSecurityAccessSetting : UdsCommonSetting
{
    /// <summary>安全等级 (奇数: 1, 3, 5...)</summary>
    public int SecurityLevel { get; set; } = 1;

    /// <summary>
    /// Key 计算表达式。运行时变量 Seed 为 byte[] 类型。
    /// 表达式应返回 byte[]。
    /// 示例: "new byte[] { (byte)(Seed[0] ^ 0xA5), (byte)(Seed[1] ^ 0x5A) }"
    /// </summary>
    [ExpressionField]
    public string KeyExpression { get; set; } = "Seed";

    /// <summary>结果变量（存储解锁状态 true/false）</summary>
    public string ResultVariable { get; set; } = "";
}
