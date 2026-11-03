using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;
using System.Text.Json.Serialization;

namespace CAN.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanReadMode { FirstMatching = 0, EachId = 1 }

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CanReadSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>一个或多个 CAN ID，以逗号、分号或空白分隔；为空则接收任意 ID。</summary>
    [ExpressionField]
    public string FilterId { get; set; } = "\"\"";

    /// <summary>首个匹配帧保持旧结果格式；每 ID 一帧输出带 ID 的完整 JSON。</summary>
    public CanReadMode ReadMode { get; set; } = CanReadMode.FirstMatching;

    /// <summary>读取超时（毫秒）</summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>结果存储变量路径（存储完整十六进制数据）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "";

    /// <summary>存储接收到的 CAN ID 的变量路径</summary>
    [VariablePathField]
    public string IdVariable { get; set; } = "";
}
