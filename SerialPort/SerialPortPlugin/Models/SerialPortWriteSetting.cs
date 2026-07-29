using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPortPlugin.Models;

[MessagePackObject(true)]
public class SerialPortWriteSetting
{
    public string PortName { get; set; } = "COM1";

    /// <summary>要发送的数据，支持表达式（运行时拼帧）</summary>
    [ExpressionField]
    public string Data { get; set; } = string.Empty;

    /// <summary>编码方式：UTF8 / ASCII / Hex</summary>
    public string Encoding { get; set; } = "ASCII";

    /// <summary>是否在末尾追加换行符</summary>
    public bool AppendNewLine { get; set; } = true;
}
