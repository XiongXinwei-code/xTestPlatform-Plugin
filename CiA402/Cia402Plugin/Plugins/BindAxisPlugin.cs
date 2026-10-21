using CiA402.Executors;
using CiA402.Models;

namespace CiA402;

[StepDefinition("BindAxis", "绑定 CiA402 轴", "- 以 AxisName 为标识名将轴会话放入运行期资源表；重名或节点已占用时报错，不替换旧会话。\n- 校验对象及 PDO 映射，不重配置驱动，不使能；正在运行的轴不能绑定。\n- EtherCAT 必须先注册主站适配工厂，未配置时报错。")]
public sealed class BindAxisPlugin : Cia402Plugin<BindAxisSetting, BindAxisExecutor> { }
