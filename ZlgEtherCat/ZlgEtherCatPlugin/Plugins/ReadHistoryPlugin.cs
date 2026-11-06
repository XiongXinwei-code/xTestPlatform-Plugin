using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ReadHistoryPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<ReadHistorySetting>
{
    public override string StepTypeId => "ZlgEtherCat.ReadHistory";
    public override string DisplayName => "ZlgEtherCat_PDO_ReadHistory";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_readhistory.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ReadHistoryExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (ReadHistorySetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                ReadHistoryOperation.ReadHistory => $"读取历史采样：连接名 {s.ConnectionName}；起始软件序号 {(string.IsNullOrWhiteSpace(s.AfterSequenceExpression) ? s.AfterSequence.ToString() : s.AfterSequenceExpression)}；结果变量 {s.ResultVariable}",
                _ => "读取历史采样（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "读取历史采样（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        读取历史采样。本步骤为固定功能，只执行 ReadHistory，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ReadHistoryOperation | 固定值，无需选择 | ReadHistory | ReadHistory=0（读取历史采样）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | ReadHistory；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | AfterSequence | int | 是 | 0 | ReadHistory；起始软件序号；long 软件样本序号的兼容常量输入（此字段保留 int 范围）；仅返回序号大于它的样本，AfterSequenceExpression 非空时优先；不使用设备周期号；范围 0～2147483647 |
        | AfterSequenceExpression | string([ExpressionField]) | 否 |  | ReadHistory；起始软件序号表达式（可选）；求值结果为 long；留空使用上方常量；非空时读取 long 软件样本序号，例如上一张样本的 Sequence，结果必须大于等于 0。 |
        | MaxSamples | int | 是 | 100 | ReadHistory；最多样本数；；范围 1～10000 |
        | TimeoutMs | int | 是 | 5000 | ReadHistory；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadHistory；结果变量；可选已声明变量完整路径；对象：Dropped、DroppedMeaning="LocalHistoryEvictions"、HardwareDroppedFrames=null、OldestSequence、Samples；Dropped 仅为本地历史缓冲淘汰数。每个样本有 Sequence、ReceivedTimestamp、Data（完整主站输入 byte[]）、QueueBacklog、LocallyValid、HasQueueEvent，以及 TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadHistory — 读取历史采样
        - 适用参数：ConnectionName、TimeoutMs、AfterSequence、AfterSequenceExpression、MaxSamples、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.StartAcquisition；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Dropped、DroppedMeaning="LocalHistoryEvictions"、HardwareDroppedFrames=null、OldestSequence、Samples；Dropped 仅为本地历史缓冲淘汰数。每个样本有 Sequence、ReceivedTimestamp、Data（完整主站输入 byte[]）、QueueBacklog、LocallyValid、HasQueueEvent，以及 TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。
        - 读取有界缓冲内样本；报告无法提供的历史范围，不伪造丢帧数。 Dropped 仅统计本地历史缓冲淘汰，SkippedSamples 仅统计读取项跳过的软件序号；二者都不是硬件丢帧计数。

        ## 相关插件
        ZlgEtherCat.StartAcquisition、ZlgEtherCat.Open。
        """;
}
