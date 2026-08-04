using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortOpenPlugin : StepPluginBase<SerialPortOpenSetting>
{
    public override string StepTypeId => "IO.SerialPortOpen";
    public override string DisplayName => "SerialPort_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description =>
        "打开指定串口并配置通信参数。打开后通过 PortName 标识连接，供后续 Write/Read/Query 使用。" +
        "Setting 字段：PortName(string,表达式,端口名如COM1), BaudRate(int,波特率,默认9600), " +
        "DataBits(int,数据位5-8,默认8), StopBits(int,停止位0=None/1=One/2=Two,默认1), " +
        "Parity(int,校验0=None/1=Odd/2=Even,默认0), " +
        "ReadTimeoutMs(int,读超时ms,默认3000), WriteTimeoutMs(int,写超时ms,默认3000)。";

    public override IStepExecutor CreateExecutor() => new SerialPortOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.PortName} @ {s.BaudRate}";
    }
}
