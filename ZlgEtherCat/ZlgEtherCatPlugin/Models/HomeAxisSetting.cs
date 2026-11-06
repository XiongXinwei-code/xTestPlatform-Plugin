using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HomeAxisOperation { HomeAxis = 0 }

[MessagePackObject(true)]
public sealed class HomeAxisSetting : ZlgGroupedSetting
{
    public HomeAxisOperation Operation { get; set; } = HomeAxisOperation.HomeAxis;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Method { get; set; } = "0";
    [ExpressionField]
    public string SearchVelocity { get; set; } = "0";
    [ExpressionField]
    public string ZeroVelocity { get; set; } = "0";
    [ExpressionField]
    public string Acceleration { get; set; } = "0";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        HomeAxisOperation.HomeAxis => "HomeAxis",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        HomeAxisOperation.HomeAxis => ["ConnectionName", "TimeoutMs", "AxisName", "Method", "SearchVelocity", "ZeroVelocity", "Acceleration", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        HomeAxisOperation.HomeAxis => new Operations.HomeAxisSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AxisName = AxisName, Method = Method, SearchVelocity = SearchVelocity, ZeroVelocity = ZeroVelocity, Acceleration = Acceleration, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
