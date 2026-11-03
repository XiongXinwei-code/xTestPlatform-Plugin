using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqImplicitTimingSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqImplicitTimingSetting() => RequestJson = "\"{\\\"sampleModeRaw\\\":10178,\\\"sampsPerChan\\\":\\\"1000\\\"}\"";
}
