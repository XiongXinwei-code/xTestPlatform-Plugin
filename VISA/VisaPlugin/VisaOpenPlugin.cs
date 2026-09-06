using VISA.Executors;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 打开会话插件，通过 Resource String 建立与仪器的连接
/// </summary>
public sealed class VisaOpenPlugin : StepPluginBase<VisaOpenSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.VisaOpen";
    public override string DisplayName => "VISA_Open";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源，打开后通过 ConnectionName 标识此连接。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | VISA1 | 连接标识名，序列内唯一 |
        | ResourceString | string([ExpressionField]) | 是 | — | VISA 资源字符串，如 TCPIP::192.168.1.1::INSTR、GPIB0::1::INSTR |
        | OpenTimeoutMs | int | 否 | 5000 | 打开超时毫秒数 |
        | IoTimeoutMs | int | 否 | 3000 | IO 超时毫秒数 |
        | Terminator | string | 否 | \n | 终止符 |

        ## 行为

        - 资源不存在或打开超时时步骤报错
        - 同名 ConnectionName 重复打开会报错，需先用 VISA_Close 关闭

        ## 检索关键词

        VISA、NI-VISA、Keysight IO Libraries、Resource String、资源字符串、
        GPIB、IEEE-488、USB-TMC、USBTMC、TCPIP、INSTR、SOCKET、ASRL、
        SCPI、仪器控制、可编程仪器、万用表、程控电源、示波器、电子负载

        ## 相关插件

        - `VISA_Write` / `VISA_Read` / `VISA_Query` / `VISA_BatchWrite` / `VISA_WaitOPC`：在此连接上操作
        - `VISA_Close`：关闭本插件打开的会话
        """;

    public override IStepExecutor CreateExecutor() => new VisaOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.ResourceString})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (VisaOpenSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_001", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_001E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.ResourceString))
            errors.Add(StepSettingError.Error("VISA_002", "VISA 资源字符串不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ResourceString, context.ExecutionContext, out var resErr))
            errors.Add(StepSettingError.Error("VISA_002E", $"ResourceString 表达式无效: {resErr}"));
        if (s.OpenTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("VISA_003", "打开超时必须大于 0"));
        if (s.IoTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("VISA_004", "IO 超时必须大于 0"));
        var term = VisaHelper.NormalizeTerminator(s.Terminator);
        if (term[^1] > 0xFF)
            errors.Add(StepSettingError.Error("VISA_005", "终止符必须是单字节字符（如 \\n、\\r\\n），不支持中文等多字节字符"));
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
