using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class StopMotionPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<StopMotionSetting>
{
    public override string StepTypeId => "ZlgEtherCat.StopMotion";
    public override string DisplayName => "ZlgEtherCat_Motion_StopMotion";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_stopmotion.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new StopMotionExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (StopMotionSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                StopMotionOperation.StopMotion => $"停止运动：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；结果变量 {s.ResultVariable}",
                _ => "停止运动（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "停止运动（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        停止运动。本步骤为固定功能，只执行 StopMotion，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | StopMotionOperation | 固定值，无需选择 | StopMotion | StopMotion=0（停止运动）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | StopMotion；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | StopMotion；轴名称；求值结果为 string；本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号 |
        | QuickStop | bool | 是 | false | StopMotion；使用快速停止； |
        | VelocityTolerance | string([ExpressionField]) | 是 | 0 | StopMotion；停稳速度容差；求值结果为 int；求值为 int，必须大于等于 0；停止确认时允许的实际速度绝对值，单位与驱动实际速度相同 |
        | TimeoutMs | int | 是 | 5000 | StopMotion；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | StopMotion；结果变量；可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### StopMotion — 停止运动
        - 适用参数：ConnectionName、TimeoutMs、AxisName、QuickStop、VelocityTolerance、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 请求 Halt 或 QuickStop 后确认反馈；CSP/CSV/CST 必须显式 QuickStop=true，驱动停止选项须正确配置。停止确认需两张不同 PDO 样本或两次 SDO 实际速度读取，仍不证明物理或功能安全停止。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ## 相关插件
        ZlgEtherCat.BindAxis、ZlgEtherCat.Open。
        """;
}
