using SerialPortPlugin.Execution;
using SerialPortPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Plugins;

public sealed class SerialPortWritePlugin : StepPluginBase<SerialPortWriteSetting>
{
    public override string StepTypeId => "SerialPort.Write";
    public override string DisplayName => "SerialPort Write";
    public override string Category => "Communication";
    public override string IconPath => string.Empty;

    public override string Description =>
        "向指定串口发送数据。Setting 字段：PortName(string,端口名), " +
        "Data(string,发送内容,支持表达式), Encoding(string,编码UTF8/ASCII/Hex), " +
        "AppendNewLine(bool,是否追加换行符)。";

    public override IStepExecutor CreateExecutor() => new SerialPortWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write → {s.PortName}: {(s.Data.Length > 30 ? s.Data[..30] + "..." : s.Data)}";
    }
}
