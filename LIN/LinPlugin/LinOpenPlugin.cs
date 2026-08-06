using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinOpenPlugin : StepPluginBase<LinOpenSetting>
{
    public override string StepTypeId   => "IO.LinOpen";
    public override string DisplayName  => "LIN_Open";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "打开 LIN 通道并建立连接，支持 LIN 1.x 和 LIN 2.x 协议，可配置为主节点或从节点模式。" +
        "Setting 字段：AdapterType(枚举,硬件类型:NI/PEAK/Vector/IXXAT,默认NI), " +
        "Channel(string,表达式,通道名称如LIN1,默认\"LIN1\"), " +
        "BaudRate(int,波特率,默认19200), " +
        "LinVersion(枚举,协议版本:LIN_1x/LIN_2x,默认LIN_2x), " +
        "IsMaster(bool,是否主节点,默认true), " +
        "ConnectionName(string,表达式,运行时连接标识名,默认\"LIN1\")。";

    public override IStepExecutor CreateExecutor() => new LinOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.AdapterType}, LIN {s.LinVersion}, {s.BaudRate} bps, {(s.IsMaster ? "主节点" : "从节点")})";
    }
}
