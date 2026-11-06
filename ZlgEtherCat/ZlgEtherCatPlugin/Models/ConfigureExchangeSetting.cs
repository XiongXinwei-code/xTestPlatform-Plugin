using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigureExchangeOperation { ConfigureExchange = 0 }

[MessagePackObject(true)]
public sealed class ConfigureExchangeSetting : ZlgGroupedSetting
{
    public ConfigureExchangeOperation Operation { get; set; } = ConfigureExchangeOperation.ConfigureExchange;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public ExchangeMode ExchangeMode { get; set; } = ExchangeMode.Queue;
    public int QueuePrefill { get; set; } = 2;
    [ExpressionField]
    public string InitialOutput { get; set; } = "new byte[0]";

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "ConfigureExchange",
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => ["ConnectionName", "TimeoutMs", "ExchangeMode", "QueuePrefill", "InitialOutput", "ResultVariable"],
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => new Operations.ConfigureExchangeSetting { ConnectionName = ConnectionName, TimeoutMs = TimeoutMs, ExchangeMode = ExchangeMode, QueuePrefill = QueuePrefill, InitialOutput = InitialOutput, ResultVariable = ResultVariable },
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
