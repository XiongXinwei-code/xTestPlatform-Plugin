using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("WriteCspTarget", "写入单次 CSP 位置目标", "- 要求实际 CSP 模式；允许在未运行使能状态提交初始目标，首个目标必须等于当前实际位置。\n- 使用已验证 RPDO 发送一次；返回不代表目标已应用或到位，SYNC 由序列编排。")]
public sealed class WriteCspTargetPlugin : Cia402Plugin<WriteCspTargetSetting, WriteCspTargetExecutor> { }
