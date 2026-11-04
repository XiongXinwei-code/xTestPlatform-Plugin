using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public class SerialPortWriteSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";

    [ExpressionField]
    public string WriteData { get; set; } = string.Empty;

    public SerialPortDataFormat DataFormat { get; set; } = SerialPortDataFormat.String;
}
