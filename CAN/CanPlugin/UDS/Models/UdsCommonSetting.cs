using MessagePack;
using xTestPlatform.Core.Models.StepSettings;
using CAN.Models;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsCommonSetting
{
    /// <summary>CAN 连接标识名（引用 CAN_Open 创建的连接）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>请求 CAN ID（Tester → ECU）</summary>
    [ExpressionField]
    public string TxId { get; set; } = "\"0x7DF\"";

    /// <summary>响应 CAN ID（ECU → Tester）</summary>
    [ExpressionField]
    public string RxId { get; set; } = "\"0x7E8\"";

    /// <summary>帧类型</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>是否使用 CAN FD 帧</summary>
    public bool UseFdFrame { get; set; } = false;

    /// <summary>响应超时 (ms)</summary>
    public int ResponseTimeoutMs { get; set; } = 5000;
    /// <summary>单个 UDS 事务总期限，包含排队、发送、接收及 NRC 0x78 等待，不是整个 Flash 步骤期限。</summary>
    public int TransactionTimeoutMs { get; set; } = 60000;
    /// <summary>ISO-TP 最大重组响应字节数；由 UDS 编辑器配置，必须大于 0，默认 4 MiB。</summary>
    public int MaxResponseBytes { get; set; } = IsoTp.IsoTpTransport.DefaultMaxReceivePayloadBytes;
}
