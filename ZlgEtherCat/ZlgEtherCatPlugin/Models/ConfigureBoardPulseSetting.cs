using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureBoardPulseOperation { ConfigureBoardPulse = 0 }

[MessagePackObject(true)]
public sealed class ConfigureBoardPulseSetting : ZlgGroupedSetting
{
    public ConfigureBoardPulseOperation Operation { get; set; } = ConfigureBoardPulseOperation.ConfigureBoardPulse;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public int PulseMode { get; set; } = 0;
    public int FrequencyHz { get; set; } = 1000;
    public int Direction { get; set; } = 0;
    public int Duty { get; set; } = 50;
    public bool Continuous { get; set; } = false;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureBoardPulseOperation.ConfigureBoardPulse => "ConfigureBoardPulse",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureBoardPulseOperation.ConfigureBoardPulse => ["ConnectionName", "TimeoutMs", "Port", "PulseMode", "FrequencyHz", "Direction", "Duty", "Continuous", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureBoardPulseOperation.ConfigureBoardPulse => new Operations.ConfigureBoardPulseSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, PulseMode = PulseMode, FrequencyHz = FrequencyHz, Direction = Direction, Duty = Duty, Continuous = Continuous, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
