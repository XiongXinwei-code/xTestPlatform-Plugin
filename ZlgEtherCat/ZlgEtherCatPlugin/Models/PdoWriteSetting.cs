using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdoWriteOperation { WritePdo = 0, WritePdoMasked = 1 }

[MessagePackObject(true)]
public sealed class PdoWriteSetting : ZlgGroupedSetting
{
    public PdoWriteOperation Operation { get; set; } = PdoWriteOperation.WritePdo;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";
    [ExpressionField]
    public string Mask { get; set; } = "new byte[] { 255 }";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        PdoWriteOperation.WritePdo => "WritePdo",
        PdoWriteOperation.WritePdoMasked => "WritePdoMasked",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        PdoWriteOperation.WritePdo => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "DataType", "Data", "ResultVariable"],
        PdoWriteOperation.WritePdoMasked => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "DataType", "Data", "Mask", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        PdoWriteOperation.WritePdo => new Operations.WritePdoSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, DataType = DataType, Data = Data, ResultVariable = ResultVariable },
        PdoWriteOperation.WritePdoMasked => new Operations.WritePdoMaskedSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, DataType = DataType, Data = Data, Mask = Mask, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
