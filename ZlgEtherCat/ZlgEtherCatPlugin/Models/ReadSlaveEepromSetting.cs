using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadSlaveEepromOperation { ReadSlaveEeprom = 0 }

[MessagePackObject(true)]
public sealed class ReadSlaveEepromSetting : ZlgGroupedSetting
{
    public ReadSlaveEepromOperation Operation { get; set; } = ReadSlaveEepromOperation.ReadSlaveEeprom;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Address { get; set; } = 0;
    public int Length { get; set; } = 2;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "ReadSlaveEeprom",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Address", "Length", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => new Operations.ReadSlaveEepromSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Address = Address, Length = Length, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
