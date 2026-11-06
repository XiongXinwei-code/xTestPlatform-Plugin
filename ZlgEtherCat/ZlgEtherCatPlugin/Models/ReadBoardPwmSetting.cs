using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadBoardPwmOperation { ReadBoardPwm = 0 }

[MessagePackObject(true)]
public sealed class ReadBoardPwmSetting : ZlgGroupedSetting
{
    public ReadBoardPwmOperation Operation { get; set; } = ReadBoardPwmOperation.ReadBoardPwm;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "ReadBoardPwm",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => ["ConnectionName", "TimeoutMs", "Port", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => new Operations.ReadBoardPwmSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Port = Port, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
