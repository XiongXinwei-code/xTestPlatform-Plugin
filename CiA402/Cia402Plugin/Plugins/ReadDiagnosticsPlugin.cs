using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("ReadDiagnostics", "读取驱动与硬件诊断", "- 至少配置一个输出；错误码使用 0x603F，硬件诊断使用绑定时显式配置的对象。\n- 未配置对象或掩码时不能输出对应判定；不从通用状态字推断硬件状态。")]
public sealed class ReadDiagnosticsPlugin : Cia402Plugin<ReadDiagnosticsSetting, ReadDiagnosticsExecutor> { }
