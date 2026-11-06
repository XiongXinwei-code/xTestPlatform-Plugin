using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadHistoryOperation { ReadHistory = 0 }

[MessagePackObject(true)]
public sealed class ReadHistorySetting : ZlgGroupedSetting
{
    public ReadHistoryOperation Operation { get; set; } = ReadHistoryOperation.ReadHistory;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int AfterSequence { get; set; } = 0;
    [ExpressionField]
    public string AfterSequenceExpression { get; set; } = "";
    public int MaxSamples { get; set; } = 100;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "ReadHistory",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadHistoryOperation.ReadHistory => ["ConnectionName", "TimeoutMs", "AfterSequence", "AfterSequenceExpression", "MaxSamples", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadHistoryOperation.ReadHistory => new Operations.ReadHistorySetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, AfterSequence = AfterSequence, AfterSequenceExpression = AfterSequenceExpression, MaxSamples = MaxSamples, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
