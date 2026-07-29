using SerialPortPlugin.Execution;
using SerialPortPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Plugins;

public sealed class SerialPortReadPlugin : StepPluginBase<SerialPortReadSetting>
{
    public override string StepTypeId => "SerialPort.Read";
    public override string DisplayName => "SerialPort Read";
    public override string Category => "Communication";
    public override string IconPath => string.Empty;

    public override string Description =>
        "从指定串口读取数据并存入目标变量。Setting 字段：PortName(string,端口名), " +
        "TargetVariable(string,结果存入变量路径), ReadMode(string,Line/Bytes/Until), " +
        "ByteCount(int,Bytes模式读取字节数), Terminator(string,Until模式终止符), " +
        "Encoding(string,编码UTF8/ASCII/Hex), TimeoutMs(int,读取超时ms)。";

    public override IStepExecutor CreateExecutor() => new SerialPortReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read ← {s.PortName} → {s.TargetVariable}";
    }
}
