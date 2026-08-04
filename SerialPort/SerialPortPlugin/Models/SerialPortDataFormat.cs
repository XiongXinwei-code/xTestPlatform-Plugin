using System.Text.Json.Serialization;

namespace SerialPort.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SerialPortDataFormat
{
    String = 0,
    Hex = 1,
    Bin = 2
}
