using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanCrcCalculateSetting
{
    [ExpressionField] public string Data { get; set; } = "\"00 00 00 00 00 00 00 00\"";
    public CanPayloadProtectionSetting Protection { get; set; } = new()
    {
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc8Autosar,
        CrcStartByte = 0, CrcEndByte = 0, CrcDataStartByte = 1, CrcDataEndByte = -1
    };
    [VariablePathField] public string ResultVariable { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
}
