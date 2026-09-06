using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpSdDiscoverPlugin : StepPluginBase<SomeIpSdDiscoverSetting>, IStepPlugin
{
    public override string StepTypeId  => "SomeIp.SdDiscover";
    public override string DisplayName => "SomeIp_SdDiscover";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | MulticastAddress | string([ExpressionField]) | 是 | "224.244.224.245" | SD 组播地址 |
        | Port | int | 否 | 30490 | SD 端口 |
        | ServiceId | string([ExpressionField]) | 否 | "0xFFFF" | 查找的服务 ID，0xFFFF 表示所有服务 |
        | TimeoutMs | int | 否 | 3000 | 收集响应超时毫秒数 |
        | ResultVariable | string(变量路径) | 否 | 空 | 存储发现结果的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时内未发现服务时步骤判定为 Failed

        ## 相关插件

        - `SomeIp_Request`：向发现的服务发送 RPC 请求
        - `SomeIp_Subscribe`：监听事件通知
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpSdDiscoverExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP-SD Discover: {s.MulticastAddress}:{s.Port} Service={s.ServiceId}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.SomeIP.Models.SomeIpSdDiscoverSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.MulticastAddress))
            errors.Add(StepSettingError.Error("SOMEIP_401", "MulticastAddress 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.MulticastAddress, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("SOMEIP_402", $"MulticastAddress 表达式无效: {e1}"));

        if (s.Port <= 0 || s.Port > 65535)
            errors.Add(StepSettingError.Error("SOMEIP_403", "Port 必须在 1~65535 之间"));

        if (string.IsNullOrWhiteSpace(s.ServiceId))
            errors.Add(StepSettingError.Error("SOMEIP_404", "ServiceId 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ServiceId, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("SOMEIP_405", $"ServiceId 表达式无效: {e2}"));

        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SOMEIP_406", "TimeoutMs 必须大于 0"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
