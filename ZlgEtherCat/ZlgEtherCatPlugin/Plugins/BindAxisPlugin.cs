using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class BindAxisPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<BindAxisSetting>
{
    public override string StepTypeId => "ZlgEtherCat.BindAxis";
    public override string DisplayName => "ZlgEtherCat_Motion_BindAxis";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_bindaxis.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new BindAxisExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (BindAxisSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                BindAxisOperation.BindAxis => $"绑定轴：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；从站 ENI 索引表达式 {s.SlaveIndex}；结果变量 {s.ResultVariable}",
                _ => "绑定轴（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "绑定轴（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        绑定轴。本步骤为固定功能，只执行 BindAxis，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | BindAxisOperation | 固定值，无需选择 | BindAxis | BindAxis=0（绑定轴）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | BindAxis；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | BindAxis；轴名称；求值结果为 string；本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号 |
        | SlaveIndex | string([ExpressionField]) | 是 | 0 | BindAxis；从站 ENI 索引表达式；求值结果为 int；求值为 int，本连接 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址或别名。范围 0～65534，先用 EnumerateSlaves 核对。；范围 0～65534 |
        | AllowedModes | string | 是 | 1 | BindAxis；允许的驱动模式；逗号分隔且不重复：1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST。仅勾选/填写驱动确实支持的模式；不配置不会自动推断 |
        | MaxFeedbackAgeMs | int | 是 | 100 | BindAxis；反馈最大本地接收年龄（ms）；从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。；范围 1～3600000 |
        | PollIntervalMs | int | 是 | 10 | BindAxis；轮询间隔（ms）；；范围 1～1000 |
        | InputByteOffset | int | 是 | 0 | BindAxis；轴输入区全局字节起点；整个主站输入映像内的绝对字节起点，按 ReadPiRange 返回值配置；后续对象偏移同为绝对值；范围 0～16777215 |
        | OutputByteOffset | int | 是 | 0 | BindAxis；轴输出区全局字节起点；整个主站输出映像内的绝对字节起点，按 ReadPiRange 返回值配置；后续对象偏移同为绝对值；范围 0～16777215 |
        | InputByteLength | int | 是 | 0 | BindAxis；轴输入区字节长度；0 表示不配置该方向 PDO，所有对应对象偏移应为 -1；非 0 必须在指定从站映像范围内。CSP/CSV/CST 需要实际 PDO 映射；范围 0～16777215 |
        | OutputByteLength | int | 是 | 0 | BindAxis；轴输出区字节长度；0 表示不配置该方向 PDO，所有对应对象偏移应为 -1；非 0 必须在指定从站映像范围内。CSP/CSV/CST 需要实际 PDO 映射；范围 0～16777215 |
        | StatusWordOffset | int | 是 | -1 | BindAxis；状态字偏移（UInt16 / 2 字节）；0x6041:00 固定 UInt16，2 字节，小端；状态位图。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6041:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ActualPositionOffset | int | 是 | -1 | BindAxis；实际位置偏移（Int32 / 4 字节）；0x6064:00 固定 Int32，4 字节，小端；驱动位置原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6064:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ActualVelocityOffset | int | 是 | -1 | BindAxis；实际速度偏移（Int32 / 4 字节）；0x606C:00 固定 Int32，4 字节，小端；驱动速度原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x606C:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ActualTorqueOffset | int | 是 | -1 | BindAxis；实际转矩偏移（Int16 / 2 字节）；0x6077:00 固定 Int16，2 字节，小端；驱动转矩原始单位，按驱动手册换算。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6077:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ModeDisplayOffset | int | 是 | -1 | BindAxis；实际模式偏移（Int8 / 1 字节）；0x6061:00 固定 Int8，1 字节，小端；CiA402 模式代码。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6061:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ControlWordOffset | int | 是 | -1 | BindAxis；控制字偏移（UInt16 / 2 字节）；0x6040:00 固定 UInt16，2 字节，小端；控制位图。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6040:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | TargetPositionOffset | int | 是 | -1 | BindAxis；目标位置偏移（Int32 / 4 字节）；0x607A:00 固定 Int32，4 字节，小端；驱动位置原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x607A:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | TargetVelocityOffset | int | 是 | -1 | BindAxis；目标速度偏移（Int32 / 4 字节）；0x60FF:00 固定 Int32，4 字节，小端；驱动速度原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x60FF:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | TargetTorqueOffset | int | 是 | -1 | BindAxis；目标转矩偏移（Int16 / 2 字节）；0x6071:00 固定 Int16，2 字节，小端；驱动转矩原始单位，按驱动手册换算。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6071:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | ModeCommandOffset | int | 是 | -1 | BindAxis；设定模式偏移（Int8 / 1 字节）；0x6060:00 固定 Int8，1 字节，小端；CiA402 模式代码。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6060:00，仍需用户按驱动字典核对。；范围 -1～16777215 |
        | TimeoutMs | int | 是 | 5000 | BindAxis；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | BindAxis；结果变量；可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### BindAxis — 绑定轴
        - 适用参数：ConnectionName、TimeoutMs、AxisName、SlaveIndex、AllowedModes、MaxFeedbackAgeMs、PollIntervalMs、InputByteOffset、OutputByteOffset、InputByteLength、OutputByteLength、StatusWordOffset、ActualPositionOffset、ActualVelocityOffset、ActualTorqueOffset、ModeDisplayOffset、ControlWordOffset、TargetPositionOffset、TargetVelocityOffset、TargetTorqueOffset、ModeCommandOffset、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 同一连接内 AxisName 与 SlaveIndex 均须未被其他轴占用；当前实现一个从站只绑定一个轴。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
