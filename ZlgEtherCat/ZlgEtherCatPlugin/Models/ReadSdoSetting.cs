using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadSdoOperation { ReadSdo = 0 }

[MessagePackObject(true)]
public sealed class ReadSdoSetting : ZlgGroupedSetting
{
    public ReadSdoOperation Operation { get; set; } = ReadSdoOperation.ReadSdo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Index { get; set; } = 4096;
    public int SubIndex { get; set; } = 0;
    public bool CompleteAccess { get; set; } = false;
    public int Capacity { get; set; } = 4;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadSdoOperation.ReadSdo => "ReadSdo",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadSdoOperation.ReadSdo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Index", "SubIndex", "CompleteAccess", "Capacity", "DataType", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadSdoOperation.ReadSdo => new Operations.ReadSdoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Index = Index, SubIndex = SubIndex, CompleteAccess = CompleteAccess, Capacity = Capacity, DataType = DataType, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
