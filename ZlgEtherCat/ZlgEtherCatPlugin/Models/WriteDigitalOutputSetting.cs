using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteDigitalOutputOperation { WriteDigitalOutput = 0 }

[MessagePackObject(true)]
public sealed class WriteDigitalOutputSetting : ZlgGroupedSetting
{
    public WriteDigitalOutputOperation Operation { get; set; } = WriteDigitalOutputOperation.WriteDigitalOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int BitOffset { get; set; } = 0;
    public bool Value { get; set; } = false;
    [ExpressionField]
    public string ValueExpression { get; set; } = "";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "WriteDigitalOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "BitOffset", "Value", "ValueExpression", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => new Operations.WriteDigitalOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, BitOffset = BitOffset, Value = Value, ValueExpression = ValueExpression, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
