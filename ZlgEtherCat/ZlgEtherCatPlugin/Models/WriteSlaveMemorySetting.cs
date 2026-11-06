using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteSlaveMemoryOperation { WriteSlaveMemory = 0 }

[MessagePackObject(true)]
public sealed class WriteSlaveMemorySetting : ZlgGroupedSetting
{
    public WriteSlaveMemoryOperation Operation { get; set; } = WriteSlaveMemoryOperation.WriteSlaveMemory;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Address { get; set; } = 0;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "WriteSlaveMemory",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Address", "Data", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => new Operations.WriteSlaveMemorySetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Address = Address, Data = Data, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
