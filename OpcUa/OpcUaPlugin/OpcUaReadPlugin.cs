using OpcUa.Helpers;
using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 读取节点插件</summary>
public sealed class OpcUaReadPlugin : StepPluginBase<OpcUaReadSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.Read";
    public override string DisplayName => "OpcUa_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        读取 OPC UA 服务器中单个节点的值，并将结果存入指定变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=Temperature |
        | ResultVariable | string(变量路径) | 是 | — | 结果存入的变量名 |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        ## 行为

        - 连接不存在、节点无效或读取超时时步骤报错

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_BatchRead`：批量读取多个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read {s.NodeId} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_020", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("OPCUA_020E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_021", "节点 ID 不能为空"));
        else if (!OpcUaHelper.IsValidNodeId(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_021F", "节点 ID 格式无效，正确格式如 ns=2;s=MyVariable"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_022", "结果变量名不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_023", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        if (s.TimeoutMs == 0 || s.TimeoutMs < -1)
            errors.Add(StepSettingError.Error("OPCUA_024", "超时必须大于 0，或为 -1 表示永不超时"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
