using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("Enable", "使能驱动", "- 按实际状态逐级转换，每次命令后等待反馈；不自动故障复位或从 Quick Stop Active 恢复。\n- 周期模式必须先配置并提交初始目标，按设备要求编排 SYNC。")]
public sealed class EnablePlugin : Cia402Plugin<EnableSetting, EnableExecutor> { }
