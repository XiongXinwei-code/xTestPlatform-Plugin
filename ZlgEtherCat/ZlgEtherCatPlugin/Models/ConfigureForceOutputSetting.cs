using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureForceOutputOperation { ConfigureForceOutput = 0 }

[MessagePackObject(true)]
public sealed class ConfigureForceOutputSetting : ZlgGroupedSetting
{
    public ConfigureForceOutputOperation Operation { get; set; } = ConfigureForceOutputOperation.ConfigureForceOutput;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureForceOutputOperation.ConfigureForceOutput => "ConfigureForceOutput",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureForceOutputOperation.ConfigureForceOutput => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Offset", "Data", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureForceOutputOperation.ConfigureForceOutput => new Operations.ConfigureForceOutputSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Offset = Offset, Data = Data, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
