using MessagePack;

namespace CAN.XCP.Models;

/// <summary>XCP_Disconnect 步骤设置</summary>
[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class XcpDisconnectSetting : XcpCommonSetting
{
}
