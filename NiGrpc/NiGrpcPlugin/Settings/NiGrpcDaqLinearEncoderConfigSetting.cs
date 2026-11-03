using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqLinearEncoderConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqLinearEncoderConfigSetting() => RequestJson = "\"{\\\"counter\\\":\\\"Dev1/ctr0\\\",\\\"decodingTypeRaw\\\":10092,\\\"zidxEnable\\\":false,\\\"zidxVal\\\":0,\\\"zidxPhaseRaw\\\":10040,\\\"unitsRaw\\\":10219,\\\"distPerPulse\\\":0.001,\\\"initialPos\\\":0}\"";
}
