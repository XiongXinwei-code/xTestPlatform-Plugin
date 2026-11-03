using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqWriteCounterFrequencySetting : NiGrpcRequestSetting
{
    public NiGrpcDaqWriteCounterFrequencySetting() => RequestJson = "\"{\\\"numSampsPerChan\\\":1,\\\"autoStart\\\":false,\\\"timeout\\\":1,\\\"dataLayoutRaw\\\":0,\\\"frequency\\\":[1000],\\\"dutyCycle\\\":[0.5]}\"";
}
