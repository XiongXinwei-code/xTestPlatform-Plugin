using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_TcpReceive 步骤设置</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public class TcpReceiveSetting
{
    /// <summary>启用时以分隔符分帧；否则保留 ExpectedLength 的既有语义。</summary>
    public bool UseDelimiter { get; set; }
    [ExpressionField] public string DelimiterHex { get; set; } = "\"0D 0A\"";
    public bool IncludeDelimiter { get; set; }
    /// <summary>单条 TCP 响应上限；分隔符本身计入。必须为 1~16777216。</summary>
    public int MaxBytes { get; set; } = 1048576;
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"TCP1\"";

    /// <summary>期望接收字节数（0 = 接收到任意数据即返回）</summary>
    public int ExpectedLength { get; set; } = 0;

    /// <summary>接收超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>数据编码格式（结果以此格式存入变量）</summary>
    public EthernetDataEncoding Encoding { get; set; } = EthernetDataEncoding.Hex;

    /// <summary>可选结果变量完整路径；非空时须已声明为 string（当前值允许 null），通信前预检。</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
