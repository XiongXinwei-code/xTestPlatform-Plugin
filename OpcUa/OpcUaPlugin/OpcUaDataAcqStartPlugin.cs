using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 数据采集启动插件</summary>
public sealed class OpcUaDataAcqStartPlugin : StepPluginBase<OpcUaDataAcqStartSetting>
{
    public override string StepTypeId => "OpcUa.DataAcqStart";
    public override string DisplayName => "OpcUa_DataAcq_Start";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_data_acq_start.png";

    public override string Description => """
        ## 功能

        启动 OPC UA 后台数据采集任务，按指定采样间隔定时读取多个节点并写入有界 FIFO 缓冲（仿硬件采集卡模式），
        由 DataAcq_Read 消费读取，DataAcq_Stop 停止。缓冲满时溢出停止采集，Read 步骤将报错。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "DataAcq1" | 采集任务标识名，序列内唯一，求值结果为 string |
        | ConnectionName | string([ExpressionField]) | 是 | "OpcUa1" | 已建立的 OPC UA 连接名，求值结果为 string |
        | Items | 集合 | 是 | — | 采集节点列表，元素字段见下方 |
        | SamplingIntervalMs | int | 否 | 100 | 采样间隔毫秒数 |
        | MaxDurationMs | int | 否 | 0 | 最大采集时长，0 表示无限；到时后采集自动停止，已缓存数据仍可读取 |
        | BufferSize | int | 否 | 10000 | FIFO 缓冲区容量（条数），满时溢出停止采集 |
        | TimeoutMs | int | 否 | 5000 | 整个步骤等待的总期限（毫秒），包含旧资源收尾及本次 SDK 操作 |

        Items 元素字段：

        - NodeId：string，节点标识（普通文本，如 ns=2;s=Temperature）
        - ColumnName：string，该节点在采集数据中的列名（普通文本，如 Temp），为空时使用 NodeId 作为列名

        ## 行为

        - 步骤启动采集后立即返回，采集在后台持续进行
        - Items 为空或连接名未找到时步骤报错
        - 后台读取失败或质量异常会记录诊断，不将该批伪装成有效数据；后续轮询仍可恢复。缓冲溢出则停止采集，Read 可查询任务健康状态及诊断；Stop 是否成功以真实采集、消费及取消回调退出为准，不把已恢复的历史读取错误等同于释放失败
        - 采集任务会以 `TaskName` 为标识名注册到运行期资源表（同时以同一标识名注册节点列表配置），供 OpcUa_DataAcq_Read / OpcUa_DataAcq_Stop 步骤取用
        - 用同一个 TaskName 重复启动前，必须在总期限内确认旧任务退出，失败则不启动新任务

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_DataAcq_Read`：从 FIFO 缓冲读取（消费）采集数据
        - `OpcUa_DataAcq_Stop`：停止采集任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaDataAcqStartExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Start: {s.TaskName} ({s.Items.Count} nodes @ {s.SamplingIntervalMs}ms)";
    }
}
