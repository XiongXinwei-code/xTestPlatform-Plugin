using CAN.Models;
using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CanWriteSetting
{
    public bool UseMessageList { get; set; }
    public ObservableCollection<CanWriteMessageItem> Messages { get; set; } = [];
    /// <summary>整个步骤求值、排队、发送等待的总期限；驱动提交成功不等于已上总线。</summary>
    public int TimeoutMs { get; set; } = 5000;
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>CAN ID（支持表达式，如 0x7DF）</summary>
    [ExpressionField]
    public string CanId { get; set; } = "\"0x7DF\"";

    /// <summary>帧类型</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>发送数据（十六进制字符串，如 "02 10 01 00 00 00 00 00"）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"\"";

    /// <summary>是否使用 CAN FD 帧格式发送</summary>
    public bool UseFdFrame { get; set; } = false;

    /// <summary>可选载荷 RC 与 CRC；旧序列默认关闭。</summary>
    public CanPayloadProtectionSetting Protection { get; set; } = new();
}
