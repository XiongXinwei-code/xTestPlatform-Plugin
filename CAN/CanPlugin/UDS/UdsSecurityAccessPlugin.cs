using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsSecurityAccessPlugin : StepPluginBase<UdsSecurityAccessSetting>
{
    public override string StepTypeId => "UDS.SecurityAccess";
    public override string DisplayName => "UDS_SecurityAccess";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU。" +
        "自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。" +
        "Setting 字段：SecurityLevel(int,安全等级1/2/3...), " +
        "KeyExpression(string,表达式,Key计算 变量Seed为byte[]), " +
        "ResultVariable(string,存储解锁结果bool), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsSecurityAccessExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SecurityAccess Level={s.SecurityLevel} (TX={s.TxId}, RX={s.RxId})";
    }
}
