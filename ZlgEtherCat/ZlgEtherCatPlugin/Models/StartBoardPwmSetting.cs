using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartBoardPwmOperation { StartBoardPwm = 0 }

[MessagePackObject(true)]
public sealed class StartBoardPwmSetting : ZlgGroupedSetting
{
    public StartBoardPwmOperation Operation { get; set; } = StartBoardPwmOperation.StartBoardPwm;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public int FrequencyHz { get; set; } = 1000;
    public int Duty { get; set; } = 50;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "StartBoardPwm",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => ["ConnectionName", "TimeoutMs", "Port", "FrequencyHz", "Duty", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => new Operations.StartBoardPwmSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, FrequencyHz = FrequencyHz, Duty = Duty, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
