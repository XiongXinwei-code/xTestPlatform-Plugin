using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.XCP;

public sealed class XcpShortUploadPlugin : StepPluginBase<XcpShortUploadSetting>, IStepPlugin
{
    public override string StepTypeId  => "XCP.ShortUpload";
    public override string DisplayName => "XCP_ShortUpload";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 XCP SHORT_UPLOAD 命令从 ECU 内存地址读取最多 7 字节数据。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | "0x7E1" | XCP 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | "0x7E9" | XCP 响应 CAN ID |
        | TimeoutMs | int | 否 | 1000 | 响应超时毫秒数 |
        | Address | string([ExpressionField]) | 是 | "0x40001000" | ECU 内存地址 |
        | AddressExtension | 枚举 | 否 | None | 可选值：None, Odt, Daq |
        | ReadLength | int | 否 | 4 | 读取字节数 1-7 |
        | ByteOrder | 枚举 | 否 | LittleEndian | 可选值：LittleEndian, BigEndian |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制数据） |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 需先通过 XCP_Connect 建立连接；从站返回错误或超时时步骤报错

        ## 相关插件

        - `XCP_Connect`：建立 XCP 连接
        - `XCP_ShortDownload`：向 ECU 内存写入数据
        """;

    public override IStepExecutor CreateExecutor() => new XcpShortUploadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP ShortUpload Addr={s.Address} Len={s.ReadLength}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (XcpShortUploadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("XCP_201", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("XCP_202", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("XCP_203", "TX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("XCP_204", "RX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.Address))
            errors.Add(StepSettingError.Error("XCP_205", "地址不能为空"));
        if (s.ReadLength is < 1 or > 7)
            errors.Add(StepSettingError.Error("XCP_206", "读取字节数必须在 1-7 之间"));

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
