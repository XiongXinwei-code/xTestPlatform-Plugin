using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteSdoOperation { WriteSdo = 0 }

[MessagePackObject(true)]
public sealed class WriteSdoSetting : ZlgGroupedSetting
{
    public WriteSdoOperation Operation { get; set; } = WriteSdoOperation.WriteSdo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Index { get; set; } = 4096;
    public int SubIndex { get; set; } = 0;
    public bool CompleteAccess { get; set; } = false;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";
    public bool VerifyReadback { get; set; } = false;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WriteSdoOperation.WriteSdo => "WriteSdo",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WriteSdoOperation.WriteSdo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Index", "SubIndex", "CompleteAccess", "DataType", "Data", "VerifyReadback", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WriteSdoOperation.WriteSdo => new Operations.WriteSdoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Index = Index, SubIndex = SubIndex, CompleteAccess = CompleteAccess, DataType = DataType, Data = Data, VerifyReadback = VerifyReadback, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
