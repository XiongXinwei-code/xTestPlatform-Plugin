using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpDisconnectPlugin : StepPluginBase<XcpDisconnectSetting>
{
    public override string StepTypeId  => "XCP.Disconnect";
    public override string DisplayName => "XCP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "断开 XCP on CAN 连接，向从站发送 DISCONNECT 命令。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名,默认\"CAN1\"), " +
        "TxId(string,表达式,XCP请求CAN ID,默认\"0x7E1\"), " +
        "RxId(string,表达式,XCP响应CAN ID,默认\"0x7E9\"), " +
        "TimeoutMs(int,响应超时毫秒,默认1000), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new XcpDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP Disconnect TX={s.TxId} RX={s.RxId}";
    }
}
