using System.Reflection;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute(string label, string description) : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
    public bool Required { get; set; }
    public double Min { get; set; } = long.MinValue;
    public double Max { get; set; } = long.MaxValue;
    public Type ResultType { get; set; } = typeof(long);
    public Type? OutputType { get; set; }
    public string Backend { get; set; } = "";
}

public sealed record SettingField(PropertyInfo Property, FieldAttribute Metadata)
{
    public bool IsExpression => Property.IsDefined(typeof(ExpressionFieldAttribute));
    public bool IsOutput => Property.IsDefined(typeof(VariablePathFieldAttribute));
    public bool IsActive(AxisSetting setting) => Metadata.Backend.Length == 0 ||
        setting is not BindAxisSetting bind || Metadata.Backend == bind.Transport.ToString();
}

public static class SettingFields
{
    public static IReadOnlyList<SettingField> Get(Type type) => type.GetProperties()
        .Select(p => (Property: p, Metadata: p.GetCustomAttribute<FieldAttribute>()))
        .Where(x => x.Metadata != null)
        .Select(x => new SettingField(x.Property, x.Metadata!)).ToArray();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransportKind { CANopen, EtherCAT }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationMode : sbyte { PP = 1, PV = 3, PT = 4, HM = 6, CSP = 8, CSV = 9, CST = 10 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriveState
{
    Unknown, NotReadyToSwitchOn, SwitchOnDisabled, ReadyToSwitchOn, SwitchedOn,
    OperationEnabled, QuickStopActive, FaultReactionActive, Fault
}
