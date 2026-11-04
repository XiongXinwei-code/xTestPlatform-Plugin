using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class UdpOpenSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"UDP1\"";
    [ExpressionField] public string LocalAddress { get; set; } = "\"0.0.0.0\"";
    public int LocalPort { get; set; } = 30490;
    public bool EnableBroadcast { get; set; }
    public int TimeoutMs { get; set; } = 3000;
    [VariablePathField] public string LocalEndpointVariable { get; set; } = string.Empty;
}
