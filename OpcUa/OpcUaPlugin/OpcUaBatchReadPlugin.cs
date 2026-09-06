using OpcUa.Helpers;
using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 批量读取插件</summary>
public sealed class OpcUaBatchReadPlugin : StepPluginBase<OpcUaBatchReadSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.BatchRead";
    public override string DisplayName => "OpcUa_BatchRead";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        批量读取 OPC UA 服务器中多个节点的值，每个节点的结果分别存入对应变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | Items | 集合 | 是 | — | 节点读取列表，元素结构见示例 |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        ## 行为

        - 一次请求批量读取所有节点，任意节点读取失败则步骤报错

        ## 示例

        ```json
        {
          "ConnectionName": "\"OpcUa1\"",
          "Items": [
            { "NodeId": "ns=2;s=Temperature", "ResultVariable": "Locals.temp_value" }
          ],
          "TimeoutMs": 5000
        }
        ```

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Read`：读取单个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaBatchReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchRead {s.Items.Count} nodes via {s.ConnectionName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaBatchReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_040", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("OPCUA_040E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_041", "节点列表为空"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (string.IsNullOrWhiteSpace(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_042", $"第 {i + 1} 行：节点标识不能为空"));
            else if (!OpcUaHelper.IsValidNodeId(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_042F", $"第 {i + 1} 行：节点标识格式无效，正确格式如 ns=2;s=MyVariable"));
            if (string.IsNullOrWhiteSpace(item.ResultVariable))
                errors.Add(StepSettingError.Error("OPCUA_043", $"第 {i + 1} 行：结果变量不能为空"));
            else if (!context.ExecutionContext.HasVariable(item.ResultVariable))
                errors.Add(StepSettingError.Error("OPCUA_044", $"第 {i + 1} 行：变量 {item.ResultVariable} 不存在，请先创建该变量"));
        }
        if (s.TimeoutMs == 0 || s.TimeoutMs < -1)
            errors.Add(StepSettingError.Error("OPCUA_045", "超时必须大于 0，或为 -1 表示永不超时"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
