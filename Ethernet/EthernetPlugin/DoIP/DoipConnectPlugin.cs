using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipConnectPlugin : StepPluginBase<DoipConnectSetting>, IStepPlugin
{
    public override string StepTypeId  => "DoIP.Connect";
    public override string DisplayName => "DoIP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        建立 DoIP（ISO 13400）TCP 连接并执行路由激活，以 SessionName 注册会话供后续步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 会话标识名 |
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.10" | DoIP 实体 IP |
        | RemotePort | string([ExpressionField]) | 是 | "13400" | TCP 端口 |
        | SourceAddress | string | 是 | "0x0E00" | 诊断仪逻辑地址 |
        | ActivationType | 枚举 | 否 | Default | 可选值：Default, WwhObd, CentralSecurity |
        | TimeoutMs | int | 否 | 3000 | 超时毫秒数 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接或路由激活失败时步骤报错

        ## 相关插件

        - `DoIP_DiagRequest`：发送 UDS 诊断请求
        - `DoIP_Disconnect`：关闭 DoIP 会话
        - `DoIP_VehicleDiscovery`：发现车辆 DoIP 实体
        """;

    public override IStepExecutor CreateExecutor() => new DoipConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Connect: {s.SessionName} -> {s.RemoteHost}:{s.RemotePort} SA={s.SourceAddress}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.DoIP.Models.DoipConnectSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.SessionName))
            errors.Add(StepSettingError.Error("DOIP_101", "SessionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SessionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("DOIP_102", $"SessionName 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.RemoteHost))
            errors.Add(StepSettingError.Error("DOIP_103", "RemoteHost 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemoteHost, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("DOIP_104", $"RemoteHost 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.RemotePort))
            errors.Add(StepSettingError.Error("DOIP_105", "RemotePort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemotePort, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("DOIP_106", $"RemotePort 表达式无效: {e3}"));

        if (string.IsNullOrWhiteSpace(s.SourceAddress))
            errors.Add(StepSettingError.Error("DOIP_107", "SourceAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SourceAddress, context.ExecutionContext, out var e4))
            errors.Add(StepSettingError.Error("DOIP_108", $"SourceAddress 表达式无效: {e4}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("DOIP_109", "TimeoutMs 必须大于 0"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
