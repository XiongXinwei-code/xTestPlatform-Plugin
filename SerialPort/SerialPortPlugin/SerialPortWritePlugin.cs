using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using SerialPort.Validation;

namespace SerialPort;

public sealed class SerialPortWritePlugin : StepPluginBase<SerialPortWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.SerialPortWrite";
    public override string DisplayName => "SerialPort_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        向已打开的串口写入数据。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 已打开的端口名 |
        | WriteData | string([ExpressionField]) | 是 | — | 要写入的数据 |
        | DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |

        ## 行为

        - DataFormat 为 Hex/Bin 时，WriteData 按十六进制/二进制文本解析后发送
        - 端口未打开或写入超时时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        - `SerialPort_Read`：读取响应
        - `SerialPort_Query`：写入+读取一体操作
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write -> {s.PortName} ({s.DataFormat})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var serializer = CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (SerialPortWriteSetting)serializer.Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion)
            : new SerialPortWriteSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_020", "PortName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portErr))
            errors.Add(StepSettingError.Error("SP_020E", $"PortName 表达式无效: {portErr}"));

        if (string.IsNullOrWhiteSpace(s.WriteData))
            errors.Add(StepSettingError.Error("SP_021", "WriteData 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.WriteData, context.ExecutionContext, out var dataErr))
            errors.Add(StepSettingError.Error("SP_021E", $"WriteData 表达式无效: {dataErr}"));

        if (s.DataFormat == SerialPortDataFormat.Hex && !string.IsNullOrWhiteSpace(s.WriteData))
        {
            var hex = s.WriteData.Trim().Replace(" ", "");
            if (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, @"^[0-9A-Fa-f]+$"))
                errors.Add(StepSettingError.Warning("SP_022", "HEX 格式数据应为偶数位十六进制字符串（如 48656C6C6F）"));
        }

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
