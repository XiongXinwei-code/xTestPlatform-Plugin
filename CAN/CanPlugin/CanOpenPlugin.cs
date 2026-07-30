using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanOpenPlugin : StepPluginBase<CanOpenSetting>
{
    public override string StepTypeId => "IO.CanOpen";
    public override string DisplayName => "CAN_Open";
    public override string Category => "CAN";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD、CAN XL 协议。" +
        "Setting 字段：AdapterType(枚举,硬件类型:NI/PEAK/Vector/ZLG), Channel(string,通道名称), " +
        "BaudRate(int,仲裁段波特率), Protocol(枚举,协议类型:Classic/FD/XL), " +
        "DataBitRate(int,数据段波特率), ConnectionName(string,连接标识名)。";

    public override IStepExecutor CreateExecutor() => new CanOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var proto = s.Protocol == CanProtocolType.Classic ? "Classic" : s.Protocol == CanProtocolType.FD ? "FD" : "XL";
        return $"Open {s.ConnectionName} ({s.AdapterType}, {proto}, {s.BaudRate} bps)";
    }
}
