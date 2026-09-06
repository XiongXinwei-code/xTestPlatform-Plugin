using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using SerialPort.Validation;

namespace SerialPort;

public sealed class SerialPortClosePlugin : StepPluginBase<SerialPortCloseSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.SerialPortClose";
    public override string DisplayName => "SerialPort_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        关闭指定串口并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 要关闭的端口名，如 COM1 |

        ## 行为

        - 端口未打开时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.PortName}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var serializer = CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (SerialPortCloseSetting)serializer.Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion)
            : new SerialPortCloseSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_010", "PortName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portErr))
            errors.Add(StepSettingError.Error("SP_010E", $"PortName 表达式无效: {portErr}"));

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
