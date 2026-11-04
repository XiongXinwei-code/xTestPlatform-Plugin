using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqTimingRateSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqTimingRateSetting() => RequestJson = "\"{\\\"attributeRaw\\\":4885,\\\"value\\\":10000000}\"";
}
