using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ReadAcquisitionStatusPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<ReadAcquisitionStatusSetting>
{
    public override string StepTypeId => "ZlgEtherCat.ReadAcquisitionStatus";
    public override string DisplayName => "ZlgEtherCat_PDO_ReadAcquisitionStatus";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_readacquisitionstatus.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ReadAcquisitionStatusExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (ReadAcquisitionStatusSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                ReadAcquisitionStatusOperation.ReadAcquisitionStatus => $"读取采集状态：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "读取采集状态（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "读取采集状态（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        读取采集状态。本步骤为固定功能，只执行 ReadAcquisitionStatus，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ReadAcquisitionStatusOperation | 固定值，无需选择 | ReadAcquisitionStatus | ReadAcquisitionStatus=0（读取采集状态）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | ReadAcquisitionStatus；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | TimeoutMs | int | 是 | 5000 | ReadAcquisitionStatus；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadAcquisitionStatus；结果变量；可选已声明变量完整路径；对象：Running、Sequence、AgeMs、QueueBacklog、LocallyValid、Error、WorkerStarts、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、HardwareSampleAgeMs=null；Sequence 是 long 软件序号，AgeMs 为主机读取完成后的年龄，不含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadAcquisitionStatus — 读取采集状态
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Running、Sequence、AgeMs、QueueBacklog、LocallyValid、Error、WorkerStarts、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、HardwareSampleAgeMs=null；Sequence 是 long 软件序号，AgeMs 为主机读取完成后的年龄，不含此前 RX 排队时间。
        -  RxQueue 为最后一次观测值，须同时查看 ObservationAgeMs 与 MaxPollGapMs；SDK 阻塞时监控也会延迟。LastObservationTimestamp/LastRecoveryTimestamp 为主机队列观测/恢复时刻的单调时钟刻度；HighWatermarkEvents/BacklogWarningEvents 计告警事件段，FullQueueObservations 计满深度观测次数。高水位/持续积压状态可随观测变化，OverflowSuspected 为疑似事件锁存，HardwareDroppedFrames=null。Queue 容量未知时仍记录深度/持续积压，但禁用高水位和满判定；非 Queue 模式禁用队列告警。告警由既有后台工作循环发送，不在 SDK 工作线程中调用宿主日志；重复消息受 RxAlarmRepeatMs 节流并可合并，NotificationError 显示通知失败。 本操作只读缓存，不调用 SDK；SDK 阻塞或通道隔离时仍可查看最后观测和错误，直到连接关闭。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
