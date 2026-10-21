using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadStateSetting : AxisSetting
{
    [VariablePathField, Field("状态字输出", "已声明的完整变量路径；写入 double，留空不写", OutputType = typeof(double))] public string StatusWordVariable { get; set; } = "";
	[VariablePathField, Field("状态名称输出", "已声明的完整变量路径；写入 string，留空不写", OutputType = typeof(string))] public string StateVariable { get; set; } = "";
}
