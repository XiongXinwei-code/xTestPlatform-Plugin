using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadSlaveMemoryOperation { ReadSlaveMemory = 0 }

[MessagePackObject(true)]
public sealed class ReadSlaveMemorySetting : ZlgGroupedSetting
{
    public ReadSlaveMemoryOperation Operation { get; set; } = ReadSlaveMemoryOperation.ReadSlaveMemory;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Address { get; set; } = 0;
    public int Length { get; set; } = 2;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadSlaveMemoryOperation.ReadSlaveMemory => "ReadSlaveMemory",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadSlaveMemoryOperation.ReadSlaveMemory => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Address", "Length", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadSlaveMemoryOperation.ReadSlaveMemory => new Operations.ReadSlaveMemorySetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Address = Address, Length = Length, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
