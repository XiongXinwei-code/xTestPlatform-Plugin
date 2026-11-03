using MessagePack;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class NiGrpcDaqExportSignalSetting : NiGrpcRequestSetting
{
    public NiGrpcDaqExportSignalSetting() => RequestJson = "\"{\\\"signalIdRaw\\\":12487,\\\"outputTerminal\\\":\\\"/Dev1/PFI1\\\"}\"";
}
