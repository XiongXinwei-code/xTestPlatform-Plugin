using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteAnalogOutputOperation { WriteAnalogOutput = 0 }

[MessagePackObject(true)]
public sealed class WriteAnalogOutputSetting : ZlgGroupedSetting
{
    public WriteAnalogOutputOperation Operation { get; set; } = WriteAnalogOutputOperation.WriteAnalogOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public PdoDataType DataType { get; set; } = PdoDataType.Int16;
    public double Value { get; set; } = 0;
    [ExpressionField]
    public string ValueExpression { get; set; } = "";
    public double Scale { get; set; } = 1;
    public double Bias { get; set; } = 0;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        WriteAnalogOutputOperation.WriteAnalogOutput => "WriteAnalogOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        WriteAnalogOutputOperation.WriteAnalogOutput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "DataType", "Value", "ValueExpression", "Scale", "Bias", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        WriteAnalogOutputOperation.WriteAnalogOutput => new Operations.WriteAnalogOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, DataType = DataType, Value = Value, ValueExpression = ValueExpression, Scale = Scale, Bias = Bias, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
