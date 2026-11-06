using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadCaptureOperation { ReadCapture = 0 }

[MessagePackObject(true)]
public sealed class ReadCaptureSetting : ZlgGroupedSetting
{
    public ReadCaptureOperation Operation { get; set; } = ReadCaptureOperation.ReadCapture;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Capacity { get; set; } = 1048576;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadCaptureOperation.ReadCapture => "ReadCapture",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadCaptureOperation.ReadCapture => ["ConnectionName", "TimeoutMs", "Capacity", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadCaptureOperation.ReadCapture => new Operations.ReadCaptureSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, Capacity = Capacity, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
