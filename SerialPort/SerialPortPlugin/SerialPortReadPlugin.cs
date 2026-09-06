using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using SerialPort.Validation;

namespace SerialPort;

public sealed class SerialPortReadPlugin : StepPluginBase<SerialPortReadSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.SerialPortRead";
    public override string DisplayName => "SerialPort_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        从已打开的串口读取数据，结果存入 ResultVariable 指定的变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 已打开的端口名 |
        | DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |
        | ReadTimeoutMs | int | 否 | 3000 | 读超时毫秒数 |
        | ReadBytes | int | 否 | 0 | 读取字节数，0 表示读到终止符 |
        | Terminator | string | 否 | \n | 终止符，ReadBytes=0 时生效 |
        | ResultVariable | string(变量路径) | 是 | — | 结果存入的变量名 |

        ## 行为

        - ReadBytes>0 时读取固定字节数，否则读到 Terminator 为止
        - 读取超时或端口未打开时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        - `SerialPort_Write`：写入数据
        - `SerialPort_Query`：写入+读取一体操作
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read <- {s.PortName} ({s.DataFormat})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var serializer = CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (SerialPortReadSetting)serializer.Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion)
            : new SerialPortReadSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_030", "PortName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portErr))
            errors.Add(StepSettingError.Error("SP_030E", $"PortName 表达式无效: {portErr}"));

        if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
            errors.Add(StepSettingError.Error("SP_031", "ReadTimeout 必须大于 0，或为 -1 表示永不超时"));

        if (s.ReadBytes < 0)
            errors.Add(StepSettingError.Error("SP_032", "ReadBytes 不能为负数"));

        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("SP_033", "ResultVariable 未配置，必须指定读取结果存放的变量路径"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("SP_034", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not string)
                errors.Add(StepSettingError.Error("SP_035", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
        }

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
