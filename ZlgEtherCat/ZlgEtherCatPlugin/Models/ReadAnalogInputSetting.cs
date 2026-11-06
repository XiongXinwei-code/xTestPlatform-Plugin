using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadAnalogInputOperation { ReadAnalogInput = 0 }

[MessagePackObject(true)]
public sealed class ReadAnalogInputSetting : ZlgGroupedSetting
{
    public ReadAnalogInputOperation Operation { get; set; } = ReadAnalogInputOperation.ReadAnalogInput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public PdoDataType DataType { get; set; } = PdoDataType.Int16;
    public double Scale { get; set; } = 1;
    public double Bias { get; set; } = 0;
    public int MaxAgeMs { get; set; } = 1000;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadAnalogInputOperation.ReadAnalogInput => "ReadAnalogInput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadAnalogInputOperation.ReadAnalogInput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "DataType", "Scale", "Bias", "MaxAgeMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadAnalogInputOperation.ReadAnalogInput => new Operations.ReadAnalogInputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, DataType = DataType, Scale = Scale, Bias = Bias, MaxAgeMs = MaxAgeMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
