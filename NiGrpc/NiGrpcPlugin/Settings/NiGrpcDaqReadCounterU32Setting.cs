using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqReadCounterU32Setting : NiGrpcRequestSetting
{
    public NiGrpcDaqReadCounterU32Setting() => RequestJson = "\"{\\\"numSampsPerChan\\\":1,\\\"timeout\\\":1,\\\"fillModeRaw\\\":0,\\\"arraySizeInSamps\\\":1}\"";
}
