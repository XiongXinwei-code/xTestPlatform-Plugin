using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("Home", "执行 HM 回零", "- 要求实际 HM 模式及运行使能；设置回零参数，清除旧启动标记，等待本次回零完成并检查回零错误。\n- 回零方式按设备手册配置，方式 0 不可用于启动。")]
public sealed class HomePlugin : Cia402Plugin<HomeSetting, HomeExecutor> { }
