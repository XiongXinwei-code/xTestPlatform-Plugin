using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqCounterEdgesConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqCounterEdgesConfigSetting() => RequestJson = "\"{\\\"counter\\\":\\\"Dev1/ctr0\\\",\\\"edgeRaw\\\":10280,\\\"initialCount\\\":0,\\\"countDirectionRaw\\\":10128}\"";
}
