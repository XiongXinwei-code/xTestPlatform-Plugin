using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>, IStepPlugin
{
    public override string StepTypeId  => "Ethernet.UdpSend";
    public override string DisplayName => "Ethernet_UdpSend";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.255" | 目标 IP |
        | RemotePort | string([ExpressionField]) | 是 | "30490" | 目标端口 |
        | LocalPort | int | 否 | 0 | 本机发送端口，0=系统自动分配 |
        | Data | string([ExpressionField]) | 是 | "01 02 03" | 发送数据 |
        | Encoding | 枚举 | 否 | Hex | 数据编码格式，可选值：Hex, Utf8, Ascii |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 无需预先建立连接，发送后立即释放 Socket

        ## 相关插件

        - `Ethernet_UdpReceive`：接收 UDP 数据
        """;

    public override IStepExecutor CreateExecutor() => new UdpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Send: {s.RemoteHost}:{s.RemotePort} [{s.Encoding}]";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.UdpSendSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.RemoteHost))
            errors.Add(StepSettingError.Error("ETH_401", "RemoteHost 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemoteHost, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("ETH_402", $"RemoteHost 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.RemotePort))
            errors.Add(StepSettingError.Error("ETH_403", "RemotePort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemotePort, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("ETH_404", $"RemotePort 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Error("ETH_405", "Data 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("ETH_406", $"Data 表达式无效: {e3}"));

        if (s.LocalPort < 0 || s.LocalPort > 65535)
            errors.Add(StepSettingError.Error("ETH_407", "LocalPort 必须在 0~65535 范围内"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
