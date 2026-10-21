using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class SetModeSetting : AxisSetting
{
    [Field("运行模式", "写入 0x6060 并读取 0x6061 确认；不自动使能")] public OperationMode Mode { get; set; } = OperationMode.PP;

    [Field("允许使能态切换", "默认关闭；仅在已核实驱动支持、运动已停止且序列已停止周期目标更新时开启，不自动停轴")]
    public bool AllowWhileEnabled { get; set; }
}
