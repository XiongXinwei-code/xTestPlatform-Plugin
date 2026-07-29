using SerialPortPlugin.Execution;
using SerialPortPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Plugins;

public sealed class SerialPortClosePlugin : StepPluginBase<SerialPortCloseSetting>
{
    public override string StepTypeId => "SerialPort.Close";
    public override string DisplayName => "SerialPort Close";
    public override string Category => "Communication";
    public override string IconPath => string.Empty;

    public override string Description =>
        "关闭指定串口并释放资源。Setting 字段：PortName(string,端口名如COM1)。";

    public override IStepExecutor CreateExecutor() => new SerialPortCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.PortName}";
    }
}
