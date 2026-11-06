using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class StopCyclicOutputPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<StopCyclicOutputSetting>
{
    public override string StepTypeId => "ZlgEtherCat.StopCyclicOutput";
    public override string DisplayName => "ZlgEtherCat_PDO_StopCyclicOutput";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_stopcyclicoutput.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new StopCyclicOutputExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (StopCyclicOutputSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                StopCyclicOutputOperation.StopCyclicOutput => $"停止 PDO 周期输出：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "停止 PDO 周期输出（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "停止 PDO 周期输出（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        停止 PDO 周期输出。本步骤为固定功能，只执行 StopCyclicOutput，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | StopCyclicOutputOperation | 固定值，无需选择 | StopCyclicOutput | StopCyclicOutput=0（停止 PDO 周期输出）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | StopCyclicOutput；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | TimeoutMs | int | 是 | 5000 | StopCyclicOutput；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | StopCyclicOutput；结果变量；可选已声明变量完整路径；对象：Stopped、QueuedFramesWithdrawn；停止续发不撤回 SDK 队列中已有帧。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### StopCyclicOutput — 停止 PDO 周期输出
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.StartCyclicOutput；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Stopped、QueuedFramesWithdrawn；停止续发不撤回 SDK 队列中已有帧。
        - 停止成功表示周期输出工作已退出；存在受控轴时须先完成停轴。 本操作要求所有轴已停止、去使能并解绑；只执行 StopMotion 而保留绑定仍不能执行本步骤。

        ## 相关插件
        ZlgEtherCat.StartCyclicOutput、ZlgEtherCat.Open。
        """;
}
