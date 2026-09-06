using OpcUa.Helpers;
using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 数据采集启动插件</summary>
public sealed class OpcUaDataAcqStartPlugin : StepPluginBase<OpcUaDataAcqStartSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.DataAcqStart";
    public override string DisplayName => "OpcUa_DataAcq_Start";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        启动 OPC UA 后台数据采集任务，按指定采样间隔定时读取多个节点并写入有界 FIFO 缓冲（仿硬件采集卡模式），
        由 DataAcq_Read 消费读取，DataAcq_Stop 停止。缓冲满时溢出停止采集，Read 步骤将报错。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 采集任务标识名，序列内唯一 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | Items | 集合 | 是 | — | 采集节点列表，元素结构见示例 |
        | SamplingIntervalMs | int | 否 | 100 | 采样间隔毫秒数 |
        | MaxDurationMs | int | 否 | 0 | 最大采集时长，0 表示无限 |
        | BufferSize | int | 否 | 10000 | FIFO 缓冲区容量（条数），满时溢出停止采集 |

        ## 行为

        - 步骤启动采集后立即返回，采集在后台持续进行
        - 同名 TaskName 已在采集中时步骤报错

        ## 示例

        ```json
        {
          "TaskName": "\"acq1\"",
          "ConnectionName": "\"OpcUa1\"",
          "Items": [
            { "NodeId": "ns=2;s=Temperature", "ColumnName": "Temp" }
          ],
          "SamplingIntervalMs": 100,
          "MaxDurationMs": 0
        }
        ```

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_DataAcq_Read`：从 FIFO 缓冲读取（消费）采集数据
        - `OpcUa_DataAcq_Stop`：停止采集任务
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Start: {s.TaskName} ({s.Items.Count} nodes @ {s.SamplingIntervalMs}ms)";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqStartSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_070", "采集任务名不能为空"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_071", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_072", "采集节点列表为空"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (string.IsNullOrWhiteSpace(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_074", $"第 {i + 1} 行：节点标识不能为空"));
            else if (!OpcUaHelper.IsValidNodeId(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_074F", $"第 {i + 1} 行：节点标识格式无效，正确格式如 ns=2;s=MyVariable"));
            if (string.IsNullOrWhiteSpace(item.ColumnName))
                errors.Add(StepSettingError.Error("OPCUA_075", $"第 {i + 1} 行：列名不能为空"));
        }
        if (s.SamplingIntervalMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_073", "采样间隔必须大于 0"));
        if (s.BufferSize <= 0)
            errors.Add(StepSettingError.Error("OPCUA_076", "FIFO 缓冲区容量必须大于 0"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
