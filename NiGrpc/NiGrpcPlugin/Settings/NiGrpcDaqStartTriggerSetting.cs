using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqStartTriggerSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqStartTriggerSetting() => RequestJson = "\"{\\\"triggerSource\\\":\\\"/Dev1/PFI0\\\",\\\"triggerEdgeRaw\\\":10280}\"";
}
