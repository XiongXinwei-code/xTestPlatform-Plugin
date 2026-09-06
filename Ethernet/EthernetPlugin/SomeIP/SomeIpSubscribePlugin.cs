using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpSubscribePlugin : StepPluginBase<SomeIpSubscribeSetting>, IStepPlugin
{
    public override string StepTypeId  => "SomeIp.Subscribe";
    public override string DisplayName => "SomeIp_Subscribe";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | LocalPort | string([ExpressionField]) | 是 | "30502" | 本地监听 UDP 端口 |
        | ServiceId | string([ExpressionField]) | 是 | "0x1234" | 服务 ID 过滤 |
        | EventId | string([ExpressionField]) | 是 | "0x8001" | 事件 ID 过滤 |
        | TimeoutMs | int | 否 | 5000 | 等待通知超时毫秒数 |
        | ResultVariable | string(变量路径) | 否 | 空 | 存储通知负载的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到匹配通知时步骤判定为 Error

        ## 相关插件

        - `SomeIp_SdDiscover`：发现服务及其 Endpoint
        - `SomeIp_Request`：RPC 请求
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpSubscribeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Subscribe: 端口 {s.LocalPort} Service={s.ServiceId} Event={s.EventId}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpSubscribeSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.LocalPort))
            errors.Add(StepSettingError.Error("SOMEIP_301", "LocalPort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.LocalPort, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("SOMEIP_302", $"LocalPort 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.ServiceId))
            errors.Add(StepSettingError.Error("SOMEIP_303", "ServiceId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ServiceId, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("SOMEIP_304", $"ServiceId 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.EventId))
            errors.Add(StepSettingError.Error("SOMEIP_305", "EventId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.EventId, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("SOMEIP_306", $"EventId 表达式无效: {e3}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SOMEIP_307", "TimeoutMs 必须大于 0"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
