using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpOpenPlugin : StepPluginBase<TcpOpenSetting>, IStepPlugin
{
    public override string StepTypeId  => "Ethernet.TcpOpen";
    public override string DisplayName => "Ethernet_TcpOpen";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 连接标识名 |
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.1" | 远端 IP 地址 |
        | RemotePort | string([ExpressionField]) | 是 | "13400" | 远端端口号 |
        | ConnectTimeoutMs | int | 否 | 3000 | 连接超时毫秒数 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接失败或超时时步骤报错

        ## 相关插件

        - `Ethernet_TcpSend` / `Ethernet_TcpReceive`：收发数据
        - `Ethernet_TcpClose`：关闭连接
        """;

    public override IStepExecutor CreateExecutor() => new TcpOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Open: {s.ConnectionName} -> {s.RemoteHost}:{s.RemotePort}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (Ethernet.Models.TcpOpenSetting)CreateSerializer()
                    .Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("ETH_001", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var e1))
            errors.Add(StepSettingError.Error("ETH_002", $"ConnectionName 表达式无效: {e1}"));

        if (string.IsNullOrWhiteSpace(s.RemoteHost))
            errors.Add(StepSettingError.Error("ETH_003", "RemoteHost 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemoteHost, context.ExecutionContext, out var e2))
            errors.Add(StepSettingError.Error("ETH_004", $"RemoteHost 表达式无效: {e2}"));

        if (string.IsNullOrWhiteSpace(s.RemotePort))
            errors.Add(StepSettingError.Error("ETH_005", "RemotePort 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RemotePort, context.ExecutionContext, out var e3))
            errors.Add(StepSettingError.Error("ETH_006", $"RemotePort 表达式无效: {e3}"));

        if (s.ConnectTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("ETH_007", "连接超时时间必须大于 0"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
