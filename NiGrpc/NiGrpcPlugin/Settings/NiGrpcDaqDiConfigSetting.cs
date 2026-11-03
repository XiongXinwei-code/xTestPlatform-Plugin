using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqDiConfigSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqDiConfigSetting() => RequestJson = "\"{\\\"lines\\\":\\\"Dev1/port0/line0\\\",\\\"lineGroupingRaw\\\":0}\"";
}
