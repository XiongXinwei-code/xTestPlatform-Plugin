using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 数据采集读取插件（FIFO 消费）</summary>
public sealed class OpcUaDataAcqReadPlugin : StepPluginBase<OpcUaDataAcqReadSetting>
{
    public override string StepTypeId => "OpcUa.DataAcqRead";
    public override string DisplayName => "OpcUa_DataAcq_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_data_acq_read.png";

    public override string Description => """
        ## 功能

        从 OPC UA 轮询采集 FIFO 读取记录；可输出兼容的记录序号波形、逐点记录 JSON 和带质量/时间列的 CSV。
        读取即从缓冲中取走数据（仿硬件采集卡 FIFO 模式），可在 Start 与 Stop 之间循环调用实现边采集边读取。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的采集任务名，求值结果为 string |
        | SamplesToRead | int | 否 | -1 | 读取记录条数，-1=读取当前全部可用 |
        | TimeoutMs | int | 否 | 5000 | 本次消费排队、转换及 CSV 导出的总期限（毫秒），必须大于 0；不是每个阶段重新计时 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | Waveform 波形变量；横轴为记录序号 sample，不表示等间隔的真实时间；非 Good 值为 NaN |
        | RecordsJsonVariable | string(VariablePathField) | 否 | 空 | JSON 字符串变量，保留值、逐节点 StatusCode、SourceTimestamp、ServerTimestamp、主机接收 UTC/Stopwatch 与记录序号 |
        | HealthVariable | string(VariablePathField) | 否 | 空 | JSON 字符串变量，包含真实任务健康/退出、连续失败、最近错误与最近成功时间 |
        | SaveToFile | bool | 否 | false | 是否将数据追加保存到 CSV 文件 |
        | CsvFilePath | string([ExpressionField]) | SaveToFile=true 时 | 空 | CSV 文件路径，追加写入，求值结果为 string |

        ## 行为

        - 需先通过 OpcUa_DataAcq_Start 启动采集任务
        - 缓冲区溢出（Read 消费不及时）时步骤报错
        - 健康任务读取到 0 条记录仍通过，波形变量不更新；持续通信失败、后台异常或非 Good 数据不会假成功
        - 超时返回 Error，原调用方取消返回 Aborted；停止等待不代表实际导出已退出，未退出前继续持有消费门，后续 Read/Stop 不能越过旧事务
        - 转换失败、导出失败或尚未产生外部输出的放弃调用保留 FIFO；CSV 已实际成功提交的批次即使等待者已离开仍提交消费，避免再次导出同一批记录，晚到后台不写序列变量
        - CSV 与变量赋值不是跨资源原子事务；导出失败可能已经部分写入。此时保留 FIFO、将规范化后的目标路径标为写入结果未知，在当前插件进程中禁止同路径重试（更换采集任务也不能绕过）；保留原文件并检查 Sequence，改用新的 CSV 文件路径后重试，不假装回滚。重启进程不能证明旧文件完整，同一文件的符号/硬链接别名也不属于路径身份保证
        - 健康 JSON 包含在途消费、最近消费错误及最近成功 CSV 提交序号；同路径实际导出串行化，未退出的旧写入继续持有该路径及消费门
        - 新 CSV 包含质量和真实时间列；旧表头不会混写，请使用新文件。读取开始前校验路径，字段按 CSV 规则转义
        - 波形 SampleRate=1/XUnit=sample 表示记录序号，不用于真实时间分析；请用逐点记录或 CSV 的时间字段

        ## 相关插件

        - `OpcUa_DataAcq_Start`：启动采集任务
        - `OpcUa_DataAcq_Stop`：停止采集任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaDataAcqReadExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Read: {s.TaskName} → {s.ResultVariable}";
    }
}
