using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadDiagnosticsSetting : AxisSetting
{
    [VariablePathField, Field("错误码输出", "完整变量路径，double；配置后读取 0x603F，不支持则报错", OutputType = typeof(double))] public string ErrorCodeVariable { get; set; } = "";
	[VariablePathField, Field("硬件诊断输出", "完整变量路径，double；须在绑定轴时配置诊断对象", OutputType = typeof(double))] public string HardwareValueVariable { get; set; } = "";
	[VariablePathField, Field("硬件异常输出", "完整变量路径，bool；须配置诊断对象及非零掩码", OutputType = typeof(bool))] public string HardwareActiveVariable { get; set; } = "";
}
