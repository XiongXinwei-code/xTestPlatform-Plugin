using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("ReadMode", "读取实际运行模式", "- 读取 0x6061，输出有符号模式值，不改变驱动状态。")]
public sealed class ReadModePlugin : Cia402Plugin<ReadModeSetting, ReadModeExecutor> { }
