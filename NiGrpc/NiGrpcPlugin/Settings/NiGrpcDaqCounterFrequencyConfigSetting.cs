using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqCounterFrequencyConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqCounterFrequencyConfigSetting() => RequestJson = "\"{\\\"counter\\\":\\\"Dev1/ctr0\\\",\\\"minVal\\\":1,\\\"maxVal\\\":1000000,\\\"unitsRaw\\\":10373,\\\"edgeRaw\\\":10280,\\\"measMethodRaw\\\":10105,\\\"measTime\\\":0.1,\\\"divisor\\\":4}\"";
}
