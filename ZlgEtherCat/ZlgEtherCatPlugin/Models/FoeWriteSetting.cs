using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FoeWriteOperation { FoeWrite = 0 }

[MessagePackObject(true)]
public sealed class FoeWriteSetting : ZlgGroupedSetting
{
    public FoeWriteOperation Operation { get; set; } = FoeWriteOperation.FoeWrite;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    [ExpressionField]
    public string LocalPath { get; set; } = "\"\"";
    [ExpressionField]
    public string RemotePath { get; set; } = "\"\"";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        FoeWriteOperation.FoeWrite => "FoeWrite",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        FoeWriteOperation.FoeWrite => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "LocalPath", "RemotePath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        FoeWriteOperation.FoeWrite => new Operations.FoeWriteSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, LocalPath = LocalPath, RemotePath = RemotePath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
