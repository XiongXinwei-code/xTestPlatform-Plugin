using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadFeedbackSetting : AxisSetting
{
    [VariablePathField, Field("状态字输出", "完整变量路径，double；留空不写", OutputType = typeof(double))] public string StatusWordVariable { get; set; } = "";
	[VariablePathField, Field("位置输出", "完整变量路径，double，设备原始单位；TPDO 必须映射该值", OutputType = typeof(double))] public string PositionVariable { get; set; } = "";
	[VariablePathField, Field("速度输出", "完整变量路径，double，设备原始单位；TPDO 必须映射该值", OutputType = typeof(double))] public string VelocityVariable { get; set; } = "";
	[VariablePathField, Field("转矩输出", "完整变量路径，double，设备原始单位；TPDO 必须映射该值", OutputType = typeof(double))] public string TorqueVariable { get; set; } = "";
}
