using MessagePack;

namespace SerialPortPlugin.Models;

[MessagePackObject(true)]
public class SerialPortReadSetting
{
    public string PortName { get; set; } = "COM1";

    /// <summary>读取结果存入的目标变量路径</summary>
    public string TargetVariable { get; set; } = string.Empty;

    /// <summary>读取模式：Line / Bytes / Until</summary>
    public string ReadMode { get; set; } = "Line";

    /// <summary>Bytes 模式下要读取的字节数</summary>
    public int ByteCount { get; set; } = 64;

    /// <summary>Until 模式下的终止符</summary>
    public string Terminator { get; set; } = "\n";

    /// <summary>编码方式：UTF8 / ASCII / Hex</summary>
    public string Encoding { get; set; } = "ASCII";

    /// <summary>读取超时（毫秒），0 表示使用串口默认超时</summary>
    public int TimeoutMs { get; set; } = 0;
}
