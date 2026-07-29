using SerialPortPlugin.Execution;
using SerialPortPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Plugins;

public sealed class SerialPortOpenPlugin : StepPluginBase<SerialPortOpenSetting>
{
    public override string StepTypeId => "SerialPort.Open";
    public override string DisplayName => "SerialPort Open";
    public override string Category => "Communication";
    public override string IconPath => string.Empty;

    public override string Description =>
        "打开指定串口并配置通信参数。Setting 字段：PortName(string,端口名如COM1), " +
        "BaudRate(int,波特率), DataBits(int,数据位), StopBits(int,停止位), " +
        "Parity(int,校验位), ReadTimeoutMs(int,读超时ms), WriteTimeoutMs(int,写超时ms)。";

    public override IStepExecutor CreateExecutor() => new SerialPortOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.PortName} @ {s.BaudRate}";
    }
}
