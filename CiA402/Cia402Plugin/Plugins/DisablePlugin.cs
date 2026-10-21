using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("Disable", "释放驱动使能", "- 清除使能相关控制位，等待 Switch On Disabled；不代替需要减速过程的 StopMotion。")]
public sealed class DisablePlugin : Cia402Plugin<DisableSetting, DisableExecutor> { }
