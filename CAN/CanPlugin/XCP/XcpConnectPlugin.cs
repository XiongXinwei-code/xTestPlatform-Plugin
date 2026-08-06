using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpConnectPlugin : StepPluginBase<XcpConnectSetting>
{
    public override string StepTypeId  => "XCP.Connect";
    public override string DisplayName => "XCP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名,默认\"CAN1\"), " +
        "TxId(string,表达式,XCP请求CAN ID,默认\"0x7E1\"), " +
        "RxId(string,表达式,XCP响应CAN ID,默认\"0x7E9\"), " +
        "TimeoutMs(int,响应超时毫秒,默认1000), " +
        "ConnectMode(枚举,连接模式:Normal/UserDefined,默认Normal), " +
        "ResourceVariable(string,存储资源掩码的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new XcpConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP Connect TX={s.TxId} RX={s.RxId} ({s.ConnectMode})";
    }
}
