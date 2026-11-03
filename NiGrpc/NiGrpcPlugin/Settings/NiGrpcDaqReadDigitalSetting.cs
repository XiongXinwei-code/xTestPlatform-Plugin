using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqReadDigitalSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqReadDigitalSetting() => RequestJson = "\"{\\\"numSampsPerChan\\\":1,\\\"timeout\\\":1,\\\"fillModeRaw\\\":0,\\\"arraySizeInSamps\\\":1}\"";
}
