using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

/// <summary>例程控制类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoutineControlType
{
    Start = 0x01,
    Stop = 0x02,
    RequestResults = 0x03
}

[MessagePackObject(true)]
public class UdsRoutineControlSetting : UdsCommonSetting
{
    /// <summary>控制类型</summary>
    public RoutineControlType ControlType { get; set; } = RoutineControlType.Start;

    /// <summary>Routine ID</summary>
    [ExpressionField]
    public string RoutineId { get; set; } = "0x0203";

    /// <summary>Routine 输入参数（十六进制，可为空）</summary>
    [ExpressionField]
    public string OptionRecord { get; set; } = "";

    /// <summary>结果变量</summary>
    public string ResultVariable { get; set; } = "";
}
