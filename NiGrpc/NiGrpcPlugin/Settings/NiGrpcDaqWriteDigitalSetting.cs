using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqWriteDigitalSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqWriteDigitalSetting() => RequestJson = "\"{\\\"numSampsPerChan\\\":1,\\\"autoStart\\\":false,\\\"timeout\\\":1,\\\"dataLayoutRaw\\\":0,\\\"writeArray\\\":[0]}\"";
}
