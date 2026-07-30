using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortReadPlugin : StepPluginBase<SerialPortReadSetting>
{
    public override string StepTypeId => "IO.SerialPortRead";
    public override string DisplayName => "SerialPort_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description =>
        "从指定串口读取数据，支持String/Hex/Bin格式。读取结果存入Step.ReadData变量。Setting 字段：PortName(string,表达式,端口名), DataFormat(enum,数据格式:String/Hex/Bin), ReadTimeoutMs(int,读超时ms), ReadBytes(int,读取字节数,0为全部), Terminator(string,结束符)。";

    public override IStepExecutor CreateExecutor() => new SerialPortReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read <- {s.PortName} ({s.DataFormat})";
    }
}
