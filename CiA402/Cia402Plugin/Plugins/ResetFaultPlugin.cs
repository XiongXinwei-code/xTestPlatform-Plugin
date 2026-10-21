using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("ResetFault", "复位驱动故障", "- 仅在 Fault 或 Fault Reaction Active 状态执行；生成复位位上升沿并撤销，等待退出故障，不自动使能。")]
public sealed class ResetFaultPlugin : Cia402Plugin<ResetFaultSetting, ResetFaultExecutor> { }
