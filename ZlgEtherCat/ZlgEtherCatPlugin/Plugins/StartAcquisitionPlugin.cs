using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class StartAcquisitionPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<StartAcquisitionSetting>
{
    public override string StepTypeId => "ZlgEtherCat.StartAcquisition";
    public override string DisplayName => "ZlgEtherCat_PDO_StartAcquisition";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_startacquisition.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new StartAcquisitionExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (StartAcquisitionSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                StartAcquisitionOperation.StartAcquisition => $"启动 PDO 后台采集：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "启动 PDO 后台采集（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "启动 PDO 后台采集（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        启动 PDO 后台采集。本步骤为固定功能，只执行 StartAcquisition，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | StartAcquisitionOperation | 固定值，无需选择 | StartAcquisition | StartAcquisition=0（启动 PDO 后台采集）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | StartAcquisition；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | NativeWaitMs | int | 是 | 20 | StartAcquisition；单次队列等待（ms）；；范围 1～1000 |
        | HistoryCapacity | int | 是 | 1024 | StartAcquisition；历史缓冲容量；；范围 1～1000000 |
        | LatestOnly | bool | 是 | false | StartAcquisition；跳过积压，仅取最新；主动清理旧队列数据以优先获得较新输入；这是用户选择的软件丢弃策略，不等于硬件丢帧。监控在清旧前记录最后观测，不能证明所有总线帧均被读取。 |
        | AcquisitionThreadPriority | AcquisitionPriority | 是 | Normal | StartAcquisition；共享 SDK 线程优先级；可选值：Normal — 普通，默认值 0；AboveNormal — 高于普通，值 1；Highest — 最高，值 2；仍由 Windows 调度，不保证硬实时 作用于本通道整个共享 SDK 线程，包括输入采集、SDO 和输出，不新增 RX 线程。StartAcquisition 应用并读回确认；StopAcquisition 恢复原优先级。更改须先停止再启动；优先级不能保证 Windows 实时调度。 |
        | RxHighWatermarkPercent | int | 是 | 80 | StartAcquisition；RX 队列高水位（%）；Queue 模式且 RX 容量已知时，按容量百分比计算高水位；容量未知时不进行高水位/满判定。只描述 SDK 最后观测的深度，队列满仅提示溢出疑似，不报告硬件丢帧数。；范围 1～100 |
        | RxBacklogWarningMs | int | 是 | 10 | StartAcquisition；RX 积压告警持续时间（ms）；Queue 模式下 RX 深度持续非零的积压告警时间阈值，以主机观测为依据；容量未知时仍监测持续积压。SDK 调用阻塞时观测及告警会延迟，不是独立实时监控线程。；范围 1～60000 |
        | RxAlarmRepeatMs | int | 是 | 1000 | StartAcquisition；RX 重复告警最小间隔（ms）；本通道相邻 RX 通知（含恢复）的最小间隔；使用已有后台工作循环通知，不为告警或读取项新增线程。；范围 100～3600000 |
        | TimeoutMs | int | 是 | 5000 | StartAcquisition；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | StartAcquisition；结果变量；可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### StartAcquisition — 启动 PDO 后台采集
        - 适用参数：ConnectionName、TimeoutMs、NativeWaitMs、HistoryCapacity、LatestOnly、AcquisitionThreadPriority、RxHighWatermarkPercent、RxBacklogWarningMs、RxAlarmRepeatMs、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.EnablePi；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。
        - 每通道唯一固定采集工作循环；步骤返回后继续采集，重复启动报错；不为读取项创建线程。 要求 ENI 包含非空输入映像；当前主站必须保持 OP，RequireDc=true 时 DC 同步必须有效。优先级应用于本通道共享 SDK 线程，包含 SDO/输出访问，应用后读取实际值确认；不新增 RX 线程。采集配置改变须先停止再启动，成功启动才重置 RX 诊断。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。

        ## 相关插件
        ZlgEtherCat.EnablePi、ZlgEtherCat.Open。
        """;
}
