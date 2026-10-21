using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class SetModeSetting : AxisSetting
{
    [Field("运行模式", "写入 0x6060 并读取 0x6061 确认；不自动使能")] public OperationMode Mode { get; set; } = OperationMode.PP;
}
