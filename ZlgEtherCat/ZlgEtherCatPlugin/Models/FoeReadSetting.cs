using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FoeReadOperation { FoeRead = 0 }

[MessagePackObject(true)]
public sealed class FoeReadSetting : ZlgGroupedSetting
{
    public FoeReadOperation Operation { get; set; } = FoeReadOperation.FoeRead;
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
        FoeReadOperation.FoeRead => "FoeRead",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        FoeReadOperation.FoeRead => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "LocalPath", "RemotePath", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        FoeReadOperation.FoeRead => new Operations.FoeReadSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, LocalPath = LocalPath, RemotePath = RemotePath, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
