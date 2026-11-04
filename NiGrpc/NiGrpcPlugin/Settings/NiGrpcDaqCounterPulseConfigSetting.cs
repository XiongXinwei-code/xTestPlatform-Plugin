using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqCounterPulseConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqCounterPulseConfigSetting() => RequestJson = "\"{\\\"counter\\\":\\\"Dev1/ctr0\\\",\\\"unitsRaw\\\":10373,\\\"idleStateRaw\\\":10214,\\\"initialDelay\\\":0,\\\"freq\\\":1000,\\\"dutyCycle\\\":0.5}\"";
}
