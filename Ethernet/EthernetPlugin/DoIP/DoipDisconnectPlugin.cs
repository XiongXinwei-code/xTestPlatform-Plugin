using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDisconnectPlugin : StepPluginBase<DoipDisconnectSetting>, IStepPlugin
{
    public override string StepTypeId  => "DoIP.Disconnect";
    public override string DisplayName => "DoIP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        关闭并释放指定 SessionName 对应的 DoIP 会话。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 要关闭的会话标识名 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 关闭后该会话名不可再被其他 DoIP 步骤使用

        ## 相关插件

        - `DoIP_Connect`：建立 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => new DoipDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Disconnect: {s.SessionName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipDisconnectSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.SessionName))
            errors.Add(StepSettingError.Error("DOIP_201", "SessionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SessionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_202", $"SessionName 表达式无效: {e1}"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
