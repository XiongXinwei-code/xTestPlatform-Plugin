using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("WriteCstTarget", "写入单次 CST 转矩目标", "- 要求实际 CST 模式；允许在未运行使能状态提交初始目标，首个目标必须为零。\n- 使用已验证 RPDO 发送一次，不执行内部循环。")]
public sealed class WriteCstTargetPlugin : Cia402Plugin<WriteCstTargetSetting, WriteCstTargetExecutor> { }
