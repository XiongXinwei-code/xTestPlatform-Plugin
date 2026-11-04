using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class UdpCloseSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"UDP1\"";
    public int TimeoutMs { get; set; } = 5000;
}
