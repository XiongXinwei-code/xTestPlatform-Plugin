using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class WriteCspTargetSetting : AxisSetting
{
    [ExpressionField, Field("周期目标位置", "求值结果为整数，设备原始位置单位；仅发送一次 PDO", Required = true, Min = -2147483648, Max = 2147483647)] public string TargetPosition { get; set; } = "0";
}
