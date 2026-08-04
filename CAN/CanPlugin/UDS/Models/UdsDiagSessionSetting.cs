using MessagePack;
using System.Text.Json.Serialization;

namespace CAN.UDS.Models;

/// <summary>UDS 诊断会话类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagSessionType
{
    Default = 0x01,
    Programming = 0x02,
    Extended = 0x03
}

[MessagePackObject(true)]
public class UdsDiagSessionSetting : UdsCommonSetting
{
    /// <summary>目标会话类型</summary>
    public DiagSessionType SessionType { get; set; } = DiagSessionType.Extended;

    /// <summary>是否抑制正响应</summary>
    public bool SuppressPositiveResponse { get; set; } = false;
}
