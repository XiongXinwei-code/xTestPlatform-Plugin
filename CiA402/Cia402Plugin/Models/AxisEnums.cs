using System.Text.Json.Serialization;

namespace CiA402.Models;

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
