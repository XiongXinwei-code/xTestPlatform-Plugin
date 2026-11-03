using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CanOpenSetting
{
    /// <summary>硬件适配器类型</summary>
    public CanAdapterType AdapterType { get; set; } = CanAdapterType.NI;

    /// <summary>CAN 通道名称（如 CAN1、PCAN_USBBUS1）</summary>
    [ExpressionField]
    public string Channel { get; set; } = "\"CAN1\"";

    /// <summary>仲裁段波特率</summary>
    public int BaudRate { get; set; } = 500_000;

    /// <summary>协议类型</summary>
    public CanProtocolType Protocol { get; set; } = CanProtocolType.Classic;

    /// <summary>数据段波特率（CAN FD 有效）</summary>
    public int DataBitRate { get; set; } = 2_000_000;

    /// <summary>数据段目标采样点（百分比，仅 CAN FD 有效）；底层寄存器由适配器自动换算</summary>
    public double DataSamplePoint { get; set; } = 80.0;

    /// <summary>使能硬件内置 120 Ω 终端电阻（硬件及厂商 API 须支持）</summary>
    public bool EnableTermination { get; set; }

    /// <summary>仲裁段目标采样点（百分比）；底层寄存器由各厂商适配器自动换算</summary>
    public double ArbitrationSamplePoint { get; set; } = 80.0;

    /// <summary>接收缓冲区大小（帧数，驱动层接收队列容量）</summary>
    public int RxQueueSize { get; set; } = 8192;

    /// <summary>运行时连接标识名（供其他步骤引用）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>打开总超时：含表达式求值、生命周期排队、旧连接收尾及本次原生打开，单位毫秒，必须大于 0。</summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>ZLG 网络设备 IPv4 地址；Client 或 UDP 时必填。</summary>
    [ExpressionField]
    public string ZlgRemoteAddress { get; set; } = "\"\"";
    public int ZlgRemotePort { get; set; } = 4001;
    public int ZlgLocalPort { get; set; } = 4001;
    /// <summary>PC 端 TCP 角色：Client=0、Server=1，来自 SDK TCP_CLIENT/TCP_SERVER。</summary>
    public int ZlgNetworkWorkMode { get; set; }
    /// <summary>CANFDCOM 厂商配置文件路径，不是 COM 端口字符串。</summary>
    [ExpressionField]
    public string ZlgConfigurationPath { get; set; } = "\"\"";
    [ExpressionField]
    public string ZlgCloudUserName { get; set; } = "\"\"";
    /// <summary>推荐引用受保护的变量；插件日志不输出密码。</summary>
    [ExpressionField]
    public string ZlgCloudPassword { get; set; } = "\"\"";
    [ExpressionField]
    public string ZlgCloudHttpServer { get; set; } = "\"\"";
    public int ZlgCloudHttpPort { get; set; } = 80;
    [ExpressionField]
    public string ZlgCloudAuthServer { get; set; } = "\"\"";
    public int ZlgCloudAuthPort { get; set; } = 80;
}
