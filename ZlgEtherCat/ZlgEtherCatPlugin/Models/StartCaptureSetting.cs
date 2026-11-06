using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartCaptureOperation { StartCapture = 0 }

[MessagePackObject(true)]
public sealed class StartCaptureSetting : ZlgGroupedSetting
{
    public StartCaptureOperation Operation { get; set; } = StartCaptureOperation.StartCapture;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Capacity { get; set; } = 1048576;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        StartCaptureOperation.StartCapture => "StartCapture",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        StartCaptureOperation.StartCapture => ["ConnectionName", "TimeoutMs", "Capacity", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        StartCaptureOperation.StartCapture => new Operations.StartCaptureSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Capacity = Capacity, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
