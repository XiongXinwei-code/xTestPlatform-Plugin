using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureWatchdogOperation { ConfigureWatchdog = 0 }

[MessagePackObject(true)]
public sealed class ConfigureWatchdogSetting : ZlgGroupedSetting
{
    public ConfigureWatchdogOperation Operation { get; set; } = ConfigureWatchdogOperation.ConfigureWatchdog;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";
    [ExpressionField]
    public string Mask { get; set; } = "new byte[] { 255 }";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureWatchdogOperation.ConfigureWatchdog => "ConfigureWatchdog",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureWatchdogOperation.ConfigureWatchdog => ["ConnectionName", "TimeoutMs", "SlaveIndex", "SlaveIndexExpression", "Data", "Mask", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureWatchdogOperation.ConfigureWatchdog => new Operations.ConfigureWatchdogSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, SlaveIndex = SlaveIndex, SlaveIndexExpression = SlaveIndexExpression, Data = Data, Mask = Mask, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
