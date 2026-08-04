using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortWritePlugin : StepPluginBase<SerialPortWriteSetting>
{
    public override string StepTypeId => "IO.SerialPortWrite";
    public override string DisplayName => "SerialPort_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description =>
        "向指定串口写入数据。" +
        "Setting 字段：PortName(string,表达式,已打开的端口名), WriteData(string,表达式,写入数据), " +
        "DataFormat(枚举:String/Hex/Bin,默认String)。";

    public override IStepExecutor CreateExecutor() => new SerialPortWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write -> {s.PortName} ({s.DataFormat})";
    }
}
