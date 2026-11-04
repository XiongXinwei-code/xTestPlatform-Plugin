using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiGrpc;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcConnectSetting : NiGrpcSetting { [ExpressionField] public string Endpoint { get; set; } = "\"http://localhost:31763\""; }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqOpenSetting : NiGrpcSessionSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqAiVoltageSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqAoVoltageSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqSampleClockSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqStartSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqStopSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqReadAnalogSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDaqWriteAnalogSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetOpenSetting : NiGrpcSessionSetting { [ExpressionField] public string ConfigurationJson { get; set; } = "\"{}\""; }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetStartSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetStopSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetReadCanSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetWriteCanSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetReadStateSetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetGetPropertySetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcXnetSetPropertySetting : NiGrpcRequestSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcCloseSessionSetting : NiGrpcSessionSetting { }
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)] public sealed class NiGrpcDisconnectSetting : NiGrpcSetting { }
