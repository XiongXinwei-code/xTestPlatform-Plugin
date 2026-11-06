using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class CyclicTargetPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<CyclicTargetSetting>
{
    public override string StepTypeId => "ZlgEtherCat.CyclicTarget";
    public override string DisplayName => "ZlgEtherCat_Motion_CyclicTarget";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_cyclictarget.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new CyclicTargetExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (CyclicTargetSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                CyclicTargetOperation.WriteCspTarget => $"写入 CSP 位置目标：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；位置目标 {s.Position}；结果变量 {s.ResultVariable}",
                CyclicTargetOperation.WriteCsvTarget => $"写入 CSV 速度目标：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；速度目标 {s.Velocity}；结果变量 {s.ResultVariable}",
                CyclicTargetOperation.WriteCstTarget => $"写入 CST 转矩目标：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；转矩目标 {s.Torque}；结果变量 {s.ResultVariable}",
                _ => "同步运动目标（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "同步运动目标（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        同步运动目标。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | CyclicTargetOperation | 是 | WriteCspTarget | WriteCspTarget=0（写入 CSP 位置目标）；WriteCsvTarget=1（写入 CSV 速度目标）；WriteCstTarget=2（写入 CST 转矩目标） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | WriteCspTarget、WriteCsvTarget、WriteCstTarget；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AxisName | string([ExpressionField]) | 仅适用操作必填 | "Axis1" | WriteCspTarget、WriteCsvTarget、WriteCstTarget；轴名称；求值结果为 string；本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号 |
        | Position | string([ExpressionField]) | 仅适用操作必填 | 0 | WriteCspTarget；位置目标；求值结果为 int；求值结果为 int；驱动原始单位 |
        | Velocity | string([ExpressionField]) | 仅适用操作必填 | 0 | WriteCsvTarget；速度目标；求值结果为 int；求值结果为 int；驱动原始单位 |
        | Torque | string([ExpressionField]) | 仅适用操作必填 | 0 | WriteCstTarget；转矩目标；求值结果为 short；求值结果为 short；驱动原始单位 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | WriteCspTarget、WriteCsvTarget、WriteCstTarget；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | WriteCspTarget、WriteCsvTarget、WriteCstTarget；结果变量；可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### WriteCspTarget — 写入 CSP 位置目标
        - 适用参数：ConnectionName、TimeoutMs、AxisName、Position、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 8 / CSP；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ### WriteCsvTarget — 写入 CSV 速度目标
        - 适用参数：ConnectionName、TimeoutMs、AxisName、Velocity、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 9 / CSV；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ### WriteCstTarget — 写入 CST 转矩目标
        - 适用参数：ConnectionName、TimeoutMs、AxisName、Torque、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 10 / CST；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ## 相关插件
        ZlgEtherCat.BindAxis、ZlgEtherCat.Open。
        """;
}
