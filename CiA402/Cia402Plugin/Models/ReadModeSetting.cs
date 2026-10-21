using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadModeSetting : AxisSetting
{
    [VariablePathField, Field("实际模式输出", "已声明的完整变量路径；写入 double，留空不写", OutputType = typeof(double))] public string ModeVariable { get; set; } = "";
}
