using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class RequestStatePlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<RequestStateSetting>
{
    public override string StepTypeId => "ZlgEtherCat.RequestState";
    public override string DisplayName => "ZlgEtherCat_Master_RequestState";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_requeststate.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new RequestStateExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (RequestStateSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                RequestStateOperation.RequestState => $"切换并等待主站状态：连接名 {s.ConnectionName}；目标状态 {s.State}；结果变量 {s.ResultVariable}",
                _ => "切换并等待主站状态（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "切换并等待主站状态（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        切换并等待主站状态。本步骤为固定功能，只执行 RequestState，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | RequestStateOperation | 固定值，无需选择 | RequestState | RequestState=0（切换并等待主站状态）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | RequestState；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | State | MasterState | 是 | Operational | RequestState；目标状态；可选值：Init — INIT 初始化；PreOperational — PREOP 预运行／邮箱配置；Bootstrap — BOOT 引导／设备支持的维护；SafeOperational — SAFEOP 安全运行协议状态；Operational — OP 运行状态 |
        | PollIntervalMs | int | 是 | 20 | RequestState；状态检查间隔（ms）；；范围 1～10000 |
        | TimeoutMs | int | 是 | 5000 | RequestState；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | RequestState；结果变量；可选已声明变量完整路径；byte：已确认的 EtherCAT 状态码（INIT=1、PREOP=2、BOOT=3、SAFEOP=4、OP=8）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### RequestState — 切换并等待主站状态
        - 适用参数：ConnectionName、TimeoutMs、State、PollIntervalMs、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.StartBus；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：byte：已确认的 EtherCAT 状态码（INIT=1、PREOP=2、BOOT=3、SAFEOP=4、OP=8）。
        - 请求状态后轮询确认；未达到目标在总期限内报错。 请求非 OP 状态前必须停止、去使能并解绑所有轴；需要保持周期读取时不要在采集中切换离开 OP。 BeginExit 后只允许请求非 OP 状态；不能重新请求 OP。

        ## 相关插件
        ZlgEtherCat.StartBus、ZlgEtherCat.Open。
        """;
}
