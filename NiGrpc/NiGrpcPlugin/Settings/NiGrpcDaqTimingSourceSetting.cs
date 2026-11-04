using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqTimingSourceSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqTimingSourceSetting() => RequestJson = "\"{\\\"attributeRaw\\\":4886,\\\"value\\\":\\\"/Dev1/10MHzRefClock\\\"}\"";
}
