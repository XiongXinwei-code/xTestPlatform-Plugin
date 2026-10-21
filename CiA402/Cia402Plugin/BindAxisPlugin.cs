using CiA402.Executors;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

public sealed class BindAxisPlugin : StepPluginBase<BindAxisSetting>
{
    public override string StepTypeId => "Motion.CiA402BindAxis";
    public override string DisplayName => "CiA402_BindAxis";
    public override string Category => "Motion";
    public override string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_bindaxis.png";

    public override string Description => """
        ## 功能
        
        绑定 CiA402 轴。
        
        ## 参数
        
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | Transport | TransportKind | 否 | TransportKind.CANopen | EtherCAT 必须注册主站适配工厂 |
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 求值结果为 string；CAN_Open 连接或 EtherCAT 主站连接 |
        | NodeId | string([ExpressionField]) | 是 | 1 | 求值结果为整数；不允许广播；仅 CANopen |
        | SlaveAddress | string([ExpressionField]) | 是 | 0 | 求值结果为整数；含义由主站适配器定义；仅 EtherCAT |
        | ObjectIndexOffset | int | 否 | 0 | 标准 0x6000~0x67FF 对象的显式偏移；按设备手册配置 |
        | CommunicationTimeoutMs | int | 否 | 1000 | 阻塞通信软超时；不得无限等待 |
        | FeedbackMaxAgeMs | int | 否 | 100 | 共享接收队列中反馈的最大驻留时间；不保证硬件采样年龄 |
        | CspPdoNumber | int | 否 | 0 | 0 表示未配置；1 对应 0x1400/0x1600，绑定时核对设备映射；仅 CANopen |
        | CspPdoLayout | string | 否 | 0x607A:0:32 | 显式索引:子索引:位数，逗号分隔；仅允许目标位置及可选控制字；仅 CANopen |
        | CsvPdoNumber | int | 否 | 0 | 0 表示未配置；不自动重配驱动；仅 CANopen |
        | CsvPdoLayout | string | 否 | 0x60FF:0:32 | 显式索引:子索引:位数；仅允许目标速度及可选控制字；仅 CANopen |
        | CstPdoNumber | int | 否 | 0 | 0 表示未配置；不自动重配驱动；仅 CANopen |
        | CstPdoLayout | string | 否 | 0x6071:0:16 | 显式索引:子索引:位数；仅允许目标转矩及可选控制字；仅 CANopen |
        | FeedbackPdoNumber | int | 否 | 0 | 0 使用 SDO 顺序读取；非零使用指定 TPDO，须包含状态字；仅 CANopen |
        | FeedbackPdoLayout | string | 否 | 0x6041:0:16,0x6064:0:32,0x6077:0:16 | 允许 0x6041、0x6064、0x606C、0x6077；索引需包含轴偏移；仅 CANopen |
        | DiagnosticIndex | int | 否 | 0 | 0 表示未配置；按设备手册填写实际索引，不自动加轴偏移 |
        | DiagnosticSubIndex | int | 否 | 0 | 设备定义的诊断对象子索引 |
        | DiagnosticBytes | int | 否 | 2 | 支持 1、2、4 字节无符号值 |
        | DiagnosticMask | long | 否 | 0 | 0 只读取；非零时匹配异常值会阻止运动操作 |
        | DiagnosticActiveValue | long | 否 | 0 | (诊断值 & 掩码) 等于此值时视为硬件条件不满足 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | 求值结果为 string；必须先绑定该轴 |
        | TimeoutMs | int | 否 | 5000 | 包括通信和轮询的总超时 |
        | PollIntervalMs | int | 否 | 20 | 非实时轮询间隔，不代表完整周期 |
        
        ## 行为
        
        - 以 AxisName 为标识名将轴会话放入运行期资源表；重名或节点已占用时报错，不替换旧会话。
        - 校验对象及 PDO 映射，不重配置驱动，不使能；正在运行的轴不能绑定。
        - EtherCAT 必须先注册主站适配工厂，未配置时报错。
        
        - 正常执行返回 Completed；参数、通信、设备状态问题及超时返回 Error；用户取消返回 Aborted。
        - 仅写入用户已声明并明确配置的完整变量路径，空输出路径不写入。
        - 周期目标由测试序列循环编排，属于非实时周期控制，不保证确定性周期；不启动后台周期任务或隐式发送 SYNC。
        - 后端声明支持时，运动等待期间可由并行序列调用 CiA402 读取步骤独立请求硬件反馈；同轴仅一个控制流程，底层传输逐次协调。排队和通信受超时约束，不以缓存值替代失败读取。
        - CANopen 轴占用期间不要混用同节点原始 SDO/PDO、CAN 接收或后台周期写入。关闭 CAN 前先解除轴绑定。
        
        ## 相关插件
        
        - CiA402_BindAxis、CiA402_SetMode、CiA402_Enable
        - CiA402_ReadState、CiA402_StopMotion、CiA402_Disable、CiA402_UnbindAxis
        - CAN_Open、CANopen_SyncSend
        """;

    public override IStepExecutor CreateExecutor() => new BindAxisExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"绑定 CiA402 轴，轴：{value.AxisName}";
    }
}
