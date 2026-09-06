using OpcUa.Helpers;
using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 写入节点插件</summary>
public sealed class OpcUaWritePlugin : StepPluginBase<OpcUaWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.Write";
    public override string DisplayName => "OpcUa_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        向 OPC UA 服务器中单个节点写入指定值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=SetPoint |
        | WriteValue | string([ExpressionField]) | 是 | — | 要写入的值 |
        | DataType | 枚举 | 否 | Auto | 可选值：Auto, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        ## 行为

        - DataType=Auto 时根据节点实际类型自动转换写入值
        - 连接不存在、节点无效或写入失败时步骤报错

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_BatchWrite`：批量写入多个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.NodeId} = {s.WriteValue}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_030", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("OPCUA_030E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_031", "节点 ID 不能为空"));
        else if (!OpcUaHelper.IsValidNodeId(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_031F", "节点 ID 格式无效，正确格式如 ns=2;s=MyVariable"));
        if (string.IsNullOrWhiteSpace(s.WriteValue))
            errors.Add(StepSettingError.Error("OPCUA_032", "写入值不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.WriteValue, context.ExecutionContext, out var valErr))
            errors.Add(StepSettingError.Error("OPCUA_032E", $"WriteValue 表达式无效: {valErr}"));
        if (s.TimeoutMs == 0 || s.TimeoutMs < -1)
            errors.Add(StepSettingError.Error("OPCUA_033", "超时必须大于 0，或为 -1 表示永不超时"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
