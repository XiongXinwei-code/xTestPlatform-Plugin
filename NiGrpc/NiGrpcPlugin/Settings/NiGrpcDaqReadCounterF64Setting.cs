using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqReadCounterF64Setting : NiGrpcRequestSetting
{
    public NiGrpcDaqReadCounterF64Setting() => RequestJson = "\"{\\\"numSampsPerChan\\\":1,\\\"timeout\\\":1,\\\"fillModeRaw\\\":0,\\\"arraySizeInSamps\\\":1}\"";
}
