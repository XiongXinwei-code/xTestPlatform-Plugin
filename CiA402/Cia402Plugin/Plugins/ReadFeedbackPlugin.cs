using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("ReadFeedback", "读取轴反馈", "- 绑定时选择 SDO 顺序读取或显式 TPDO；TPDO 未映射所需输出时返回错误。\n- SDO 多对象读取不是原子快照；TPDO 检测重复硬件时间戳和接收队列驻留时间，但不能证明硬件采样年龄。\n- 没有有效反馈时超时报错，不用旧值冒充本次数据。")]
public sealed class ReadFeedbackPlugin : Cia402Plugin<ReadFeedbackSetting, ReadFeedbackExecutor> { }
