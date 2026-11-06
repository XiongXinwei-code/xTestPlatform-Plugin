using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class AxisReadPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<AxisReadSetting>
{
    public override string StepTypeId => "ZlgEtherCat.AxisRead";
    public override string DisplayName => "ZlgEtherCat_Motion_AxisRead";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_axisread.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new AxisReadExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (AxisReadSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                AxisReadOperation.ReadAxisState => $"读取轴状态：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；结果变量 {s.ResultVariable}",
                AxisReadOperation.ReadAxisMode => $"读取实际模式：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；结果变量 {s.ResultVariable}",
                AxisReadOperation.ReadAxisFeedback => $"读取综合反馈：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；结果变量 {s.ResultVariable}",
                AxisReadOperation.ReadAxisDiagnostics => $"读取轴报警诊断：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；结果变量 {s.ResultVariable}",
                _ => "轴状态与反馈读取（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "轴状态与反馈读取（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        轴状态与反馈读取。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | AxisReadOperation | 是 | ReadAxisState | ReadAxisState=0（读取轴状态）；ReadAxisMode=1（读取实际模式）；ReadAxisFeedback=2（读取综合反馈）；ReadAxisDiagnostics=3（读取轴报警诊断） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | ReadAxisState、ReadAxisMode、ReadAxisFeedback、ReadAxisDiagnostics；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AxisName | string([ExpressionField]) | 仅适用操作必填 | "Axis1" | ReadAxisState、ReadAxisMode、ReadAxisFeedback、ReadAxisDiagnostics；轴名称；求值结果为 string；本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | ReadAxisState、ReadAxisMode、ReadAxisFeedback、ReadAxisDiagnostics；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadAxisState、ReadAxisMode、ReadAxisFeedback、ReadAxisDiagnostics；结果变量；不同操作的范围、方向和返回类型见下方逐操作参数说明 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadAxisState — 读取轴状态
        - 适用参数：ConnectionName、TimeoutMs、AxisName、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；MotionDriveState：Unknown、NotReady、SwitchOnDisabled、ReadyToSwitchOn、SwitchedOn、OperationEnabled、QuickStopActive、FaultReactionActive、Fault。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionDriveState：Unknown、NotReady、SwitchOnDisabled、ReadyToSwitchOn、SwitchedOn、OperationEnabled、QuickStopActive、FaultReactionActive、Fault。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ### ReadAxisMode — 读取实际模式
        - 适用参数：ConnectionName、TimeoutMs、AxisName、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；sbyte：驱动实际模式（1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：sbyte：驱动实际模式（1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST）。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ### ReadAxisFeedback — 读取综合反馈
        - 适用参数：ConnectionName、TimeoutMs、AxisName、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：StatusWord（ushort）、Mode（sbyte?）、Position（int?）、Velocity（int?）、Torque（short?）、Sequence（long）、ReceivedTimestamp（单调时钟刻度）、TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；未映射反馈可为 null，SDO 多次读取后的主机时刻不表示原子采样。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：StatusWord（ushort）、Mode（sbyte?）、Position（int?）、Velocity（int?）、Torque（short?）、Sequence（long）、ReceivedTimestamp（单调时钟刻度）、TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；未映射反馈可为 null，SDO 多次读取后的主机时刻不表示原子采样。
        - 有 PDO 映射时读取一张完整快照；无 PDO 映射时按顺序读取 SDO，Sequence=0，数据不保证原子一致。 PDO 时间来自对应快照的主机读取完成时刻；SDO 时间在多次对象读取完成后记录，不保证各字段来自同一采样时刻。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ### ReadAxisDiagnostics — 读取轴报警诊断
        - 适用参数：ConnectionName、TimeoutMs、AxisName、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：StatusWord（ushort）、State（MotionDriveState）、Mode（sbyte）、ErrorCode（ushort，0x603F）、ErrorRegister（byte，0x1001）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：StatusWord（ushort）、State（MotionDriveState）、Mode（sbyte）、ErrorCode（ushort，0x603F）、ErrorRegister（byte，0x1001）。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 报警对象 0x603F/0x1001 需要 SDO，须在 Queue 周期输出停止后读取。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ## 相关插件
        ZlgEtherCat.BindAxis、ZlgEtherCat.Open。
        """;
}
