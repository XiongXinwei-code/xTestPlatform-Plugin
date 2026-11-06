using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class MasterDiagnosticsPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<MasterDiagnosticsSetting>
{
    public override string StepTypeId => "ZlgEtherCat.MasterDiagnostics";
    public override string DisplayName => "ZlgEtherCat_Diagnostics_MasterDiagnostics";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_masterdiagnostics.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new MasterDiagnosticsExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (MasterDiagnosticsSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                MasterDiagnosticsOperation.ReadMasterStatus => $"读取主站状态：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                MasterDiagnosticsOperation.ReadDiagnostics => $"读取综合诊断：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                MasterDiagnosticsOperation.ReadLinkDiagnostics => $"读取链路诊断：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                MasterDiagnosticsOperation.ReadLastError => $"读取 SDK 错误详情：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "主站诊断读取（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "主站诊断读取（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        主站诊断读取。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | MasterDiagnosticsOperation | 是 | ReadMasterStatus | ReadMasterStatus=0（读取主站状态）；ReadDiagnostics=1（读取综合诊断）；ReadLinkDiagnostics=2（读取链路诊断）；ReadLastError=3（读取 SDK 错误详情） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | ReadMasterStatus、ReadDiagnostics、ReadLinkDiagnostics、ReadLastError；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | ReadMasterStatus、ReadDiagnostics、ReadLinkDiagnostics、ReadLastError；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadMasterStatus、ReadDiagnostics、ReadLinkDiagnostics、ReadLastError；结果变量；不同操作的范围、方向和返回类型见下方逐操作参数说明 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadMasterStatus — 读取主站状态
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：Board、BusStatus、Running、State、RequireDc、ActiveBoardOutputs、BoardLatchOwned、BoardLatchStopUnconfirmed；State 是 EtherCAT 状态码，板载输出状态不证明电机物理停稳。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Board、BusStatus、Running、State、RequireDc、ActiveBoardOutputs、BoardLatchOwned、BoardLatchStopUnconfirmed；State 是 EtherCAT 状态码，板载输出状态不证明电机物理停稳。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ### ReadDiagnostics — 读取综合诊断
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：Raw（原始主从站诊断）、Updated、DcInternalSyncEstablished、DcExternalSyncEstablished、DcPropagationDelayInitialized。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Raw（原始主从站诊断）、Updated、DcInternalSyncEstablished、DcExternalSyncEstablished、DcPropagationDelayInitialized。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ### ReadLinkDiagnostics — 读取链路诊断
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：FirstLinkDown、Faults（链路故障及拓扑）；拓扑 PhysicalPosition 不是 SlaveIndex。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：FirstLinkDown、Faults（链路故障及拓扑）；拓扑 PhysicalPosition 不是 SlaveIndex。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ### ReadLastError — 读取 SDK 错误详情
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；string：SDK 错误文本；不是自动重试或恢复操作。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：string：SDK 错误文本；不是自动重试或恢复操作。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
