using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("ReadState", "读取驱动状态", "- 返回原始状态字及解码状态；诊断读取成功时即使驱动处于故障也返回 Completed，不改变驱动状态。")]
public sealed class ReadStatePlugin : Cia402Plugin<ReadStateSetting, ReadStateExecutor> { }
