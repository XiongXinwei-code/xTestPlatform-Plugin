using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 断开连接插件</summary>
public sealed class OpcUaDisconnectPlugin : StepPluginBase<OpcUaDisconnectSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.Disconnect";
    public override string DisplayName => "OpcUa_Disconnect";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        断开指定的 OPC UA 连接并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要断开的 OPC UA 连接标识名 |

        ## 行为

        - 连接不存在时步骤报错

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Disconnect {s.ConnectionName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaDisconnectSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_010", "连接标识名不能为空"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
