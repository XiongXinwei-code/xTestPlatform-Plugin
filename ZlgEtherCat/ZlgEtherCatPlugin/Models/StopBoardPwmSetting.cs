using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopBoardPwmOperation { StopBoardPwm = 0 }

[MessagePackObject(true)]
public sealed class StopBoardPwmSetting : ZlgGroupedSetting
{
    public StopBoardPwmOperation Operation { get; set; } = StopBoardPwmOperation.StopBoardPwm;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StopBoardPwmOperation.StopBoardPwm => "StopBoardPwm",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StopBoardPwmOperation.StopBoardPwm => ["ConnectionName", "TimeoutMs", "Port", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StopBoardPwmOperation.StopBoardPwm => new Operations.StopBoardPwmSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
