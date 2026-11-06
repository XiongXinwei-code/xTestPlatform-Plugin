using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestStateOperation { RequestState = 0 }

[MessagePackObject(true)]
public sealed class RequestStateSetting : ZlgGroupedSetting
{
    public RequestStateOperation Operation { get; set; } = RequestStateOperation.RequestState;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public MasterState State { get; set; } = MasterState.Operational;
    public int PollIntervalMs { get; set; } = 20;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        RequestStateOperation.RequestState => "RequestState",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        RequestStateOperation.RequestState => ["ConnectionName", "TimeoutMs", "State", "PollIntervalMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        RequestStateOperation.RequestState => new Operations.RequestStateSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, State = State, PollIntervalMs = PollIntervalMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
