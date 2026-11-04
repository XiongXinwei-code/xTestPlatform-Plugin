using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqAngularEncoderConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqAngularEncoderConfigSetting() => RequestJson = "\"{\\\"counter\\\":\\\"Dev1/ctr0\\\",\\\"decodingTypeRaw\\\":10092,\\\"zidxEnable\\\":false,\\\"zidxVal\\\":0,\\\"zidxPhaseRaw\\\":10040,\\\"unitsRaw\\\":10146,\\\"pulsesPerRev\\\":1024,\\\"initialAngle\\\":0}\"";
}
