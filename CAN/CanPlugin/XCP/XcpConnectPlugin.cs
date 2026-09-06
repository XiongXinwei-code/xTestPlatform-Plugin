using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.XCP;

public sealed class XcpConnectPlugin : StepPluginBase<XcpConnectSetting>, IStepPlugin
{
    public override string StepTypeId  => "XCP.Connect";
    public override string DisplayName => "XCP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | "0x7E1" | XCP 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | "0x7E9" | XCP 响应 CAN ID |
        | TimeoutMs | int | 否 | 1000 | 响应超时毫秒数 |
        | ConnectMode | 枚举 | 否 | Normal | 可选值：Normal, UserDefined |
        | ResourceVariable | string | 否 | 空 | 存储资源掩码的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接成功后可执行 ShortUpload/ShortDownload 等 XCP 操作
        - 从站无响应或返回错误时步骤报错

        ## 相关插件

        - `CAN_Open`：先打开 CAN 通道
        - `XCP_Disconnect`：断开 XCP 连接
        """;

    public override IStepExecutor CreateExecutor() => new XcpConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP Connect TX={s.TxId} RX={s.RxId} ({s.ConnectMode})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (XcpConnectSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("XCP_001", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("XCP_002", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("XCP_003", "TX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("XCP_004", "RX ID 不能为空"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("XCP_005", "超时时间必须大于 0"));

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
