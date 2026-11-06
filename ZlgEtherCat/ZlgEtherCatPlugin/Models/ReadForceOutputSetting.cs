using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadForceOutputOperation { ReadForceOutput = 0 }

[MessagePackObject(true)]
public sealed class ReadForceOutputSetting : ZlgGroupedSetting
{
    public ReadForceOutputOperation Operation { get; set; } = ReadForceOutputOperation.ReadForceOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ReadForceOutputOperation.ReadForceOutput => "ReadForceOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ReadForceOutputOperation.ReadForceOutput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ReadForceOutputOperation.ReadForceOutput => new Operations.ReadForceOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
