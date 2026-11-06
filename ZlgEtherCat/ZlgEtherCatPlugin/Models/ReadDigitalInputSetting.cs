using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadDigitalInputOperation { ReadDigitalInput = 0 }

[MessagePackObject(true)]
public sealed class ReadDigitalInputSetting : ZlgGroupedSetting
{
    public ReadDigitalInputOperation Operation { get; set; } = ReadDigitalInputOperation.ReadDigitalInput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int BitOffset { get; set; } = 0;
    public int MaxAgeMs { get; set; } = 1000;

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "ReadDigitalInput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "BitOffset", "MaxAgeMs", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => new Operations.ReadDigitalInputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, BitOffset = BitOffset, MaxAgeMs = MaxAgeMs, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
