using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class MovePositionPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<MovePositionSetting>
{
    public override string StepTypeId => "ZlgEtherCat.MovePosition";
    public override string DisplayName => "ZlgEtherCat_Motion_MovePosition";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_moveposition.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new MovePositionExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (MovePositionSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                MovePositionOperation.MovePosition => $"位置运动：连接名 {s.ConnectionName}；轴名称 {s.AxisName}；目标位置 {s.Position}；结果变量 {s.ResultVariable}",
                _ => "位置运动（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "位置运动（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        位置运动。本步骤为固定功能，只执行 MovePosition，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | MovePositionOperation | 固定值，无需选择 | MovePosition | MovePosition=0（位置运动）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | MovePosition；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AxisName | string([ExpressionField]) | 是 | "Axis1" | MovePosition；轴名称；求值结果为 string；本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号 |
        | Position | string([ExpressionField]) | 是 | 0 | MovePosition；目标位置；求值结果为 int；求值结果为 int；驱动原始单位 |
        | Relative | bool | 是 | false | MovePosition；相对运动； |
        | ProfileVelocity | string([ExpressionField]) | 是 | 0 | MovePosition；轮廓速度；求值结果为 uint；求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm |
        | Acceleration | string([ExpressionField]) | 是 | 0 | MovePosition；加速度；求值结果为 uint；求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm |
        | Deceleration | string([ExpressionField]) | 是 | 0 | MovePosition；减速度；求值结果为 uint；求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm |
        | TimeoutMs | int | 是 | 5000 | MovePosition；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | MovePosition；结果变量；可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### MovePosition — 位置运动
        - 适用参数：ConnectionName、TimeoutMs、AxisName、Position、Relative、ProfileVelocity、Acceleration、Deceleration、ResultVariable。

        - 目标与范围：目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。
        - 前置操作：ZlgEtherCat.BindAxis；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。
        - 独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 本步骤等待本次目标握手及驱动目标到达；轮廓速度、加速度和减速度必须大于 0。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 1 / PP；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。

        ## 相关插件
        ZlgEtherCat.BindAxis、ZlgEtherCat.Open。
        """;
}
