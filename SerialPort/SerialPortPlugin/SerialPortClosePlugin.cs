using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortClosePlugin : StepPluginBase<SerialPortCloseSetting>
{
    public override string StepTypeId => "IO.SerialPortClose";
    public override string DisplayName => "SerialPort_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description =>
        "关闭指定串口并释放资源。Setting 字段：PortName(string,表达式,端口名如COM1)。";

    public override IStepExecutor CreateExecutor() => new SerialPortCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.PortName}";
    }
}
