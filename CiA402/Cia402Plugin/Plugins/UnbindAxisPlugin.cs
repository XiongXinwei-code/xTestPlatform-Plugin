using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("UnbindAxis", "解除 CiA402 轴绑定", "- 查找 AxisName 对应会话，尝试停止运动并释放使能，然后移除会话；不关闭共享总线。\n- 会话不存在时不报错，返回 Completed。清理失败只记录日志。")]
public sealed class UnbindAxisPlugin : Cia402Plugin<UnbindAxisSetting, UnbindAxisExecutor> { }
