using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpFireAndForgetPlugin : StepPluginBase<SomeIpFireAndForgetSetting>, IStepPlugin
{
    public override string StepTypeId  => "SomeIp.FireAndForget";
    public override string DisplayName => "SomeIp_FireAndForget";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.20" | 服务端 IP |
        | RemotePort | string([ExpressionField]) | 是 | "30501" | 服务端端口 |
        | Transport | 枚举 | 否 | Udp | 传输方式，可选值：Udp, Tcp |
        | ServiceId | string([ExpressionField]) | 是 | "0x1234" | 服务 ID |
        | MethodId | string([ExpressionField]) | 是 | "0x0001" | 方法 ID |
        | ClientId | string([ExpressionField]) | 否 | "0x0001" | 客户端 ID |
        | InterfaceVersion | string([ExpressionField]) | 否 | "0x01" | 接口版本 |
        | Payload | string([ExpressionField]) | 否 | 空 | 十六进制负载，可为空 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 发送成功即判定为 Passed，不等待响应

        ## 相关插件

        - `SomeIp_Request`：带响应的 RPC 请求
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpFireAndForgetExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP FireAndForget: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpFireAndForgetSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        ValidateExpr(errors, context, s.RemoteHost, "RemoteHost", "SOMEIP_201", "SOMEIP_202");
        ValidateExpr(errors, context, s.RemotePort, "RemotePort", "SOMEIP_203", "SOMEIP_204");
        ValidateExpr(errors, context, s.ServiceId, "ServiceId", "SOMEIP_205", "SOMEIP_206");
        ValidateExpr(errors, context, s.MethodId, "MethodId", "SOMEIP_207", "SOMEIP_208");
        ValidateExpr(errors, context, s.ClientId, "ClientId", "SOMEIP_209", "SOMEIP_210");
        ValidateExpr(errors, context, s.InterfaceVersion, "InterfaceVersion", "SOMEIP_211", "SOMEIP_212");

        if (!string.IsNullOrWhiteSpace(s.Payload)
            && !context.Evaluator.ValidateExpression(s.Payload, context.ExecutionContext, out var pe))
            errors.Add(StepSettingError.Error("SOMEIP_213", $"Payload 表达式无效: {pe}"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }

    private static void ValidateExpr(List<StepSettingError> errors, StepSettingValidationContext context,
        string value, string fieldName, string emptyCode, string invalidCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(StepSettingError.Error(emptyCode, $"{fieldName} 不能为空"));
        else if (!context.Evaluator.ValidateExpression(value, context.ExecutionContext, out var e))
            errors.Add(StepSettingError.Error(invalidCode, $"{fieldName} 表达式无效: {e}"));
    }
}
