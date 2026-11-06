using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdoReadOperation { ReadPdo = 0, ReadPdoBits = 1 }

[MessagePackObject(true)]
public sealed class PdoReadSetting : ZlgGroupedSetting
{
    public PdoReadOperation Operation { get; set; } = PdoReadOperation.ReadPdo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int Length { get; set; } = 1;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    public int MaxAgeMs { get; set; } = 1000;
    public bool ReturnSample { get; set; } = false;
    public int BitOffset { get; set; } = 0;
    public int BitLength { get; set; } = 1;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PdoReadOperation.ReadPdo => "ReadPdo",
        PdoReadOperation.ReadPdoBits => "ReadPdoBits",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PdoReadOperation.ReadPdo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "Length", "DataType", "MaxAgeMs", "ReturnSample", "ResultVariable"],
        PdoReadOperation.ReadPdoBits => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "BitOffset", "BitLength", "MaxAgeMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PdoReadOperation.ReadPdo => new Operations.ReadPdoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, Length = Length, DataType = DataType, MaxAgeMs = MaxAgeMs, ReturnSample = ReturnSample, ResultVariable = ResultVariable },
        PdoReadOperation.ReadPdoBits => new Operations.ReadPdoBitsSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, BitOffset = BitOffset, BitLength = BitLength, MaxAgeMs = MaxAgeMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
