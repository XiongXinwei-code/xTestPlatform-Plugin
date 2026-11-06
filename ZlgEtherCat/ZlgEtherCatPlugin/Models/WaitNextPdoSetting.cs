using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WaitNextPdoOperation { WaitNextPdo = 0 }

[MessagePackObject(true)]
public sealed class WaitNextPdoSetting : ZlgGroupedSetting
{
    public WaitNextPdoOperation Operation { get; set; } = WaitNextPdoOperation.WaitNextPdo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int Length { get; set; } = 1;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    public int MaxAgeMs { get; set; } = 1000;
    public int AfterSequence { get; set; } = 0;
    [ExpressionField]
    public string AfterSequenceExpression { get; set; } = "";
    public bool ReturnSample { get; set; } = false;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "WaitNextPdo",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "Length", "DataType", "MaxAgeMs", "AfterSequence", "AfterSequenceExpression", "ReturnSample", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => new Operations.WaitNextPdoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, Length = Length, DataType = DataType, MaxAgeMs = MaxAgeMs, AfterSequence = AfterSequence, AfterSequenceExpression = AfterSequenceExpression, ReturnSample = ReturnSample, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
