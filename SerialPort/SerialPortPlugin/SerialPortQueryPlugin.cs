using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using SerialPort.Validation;

namespace SerialPort;

public sealed class SerialPortQueryPlugin : StepPluginBase<SerialPortQuerySetting>, IStepPlugin
{
	public override string StepTypeId => "IO.SerialPortQuery";
	public override string DisplayName => "SerialPort_Query";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

	public override string Description => """
		## 功能

		向已打开的串口发送数据并读取响应（Write+Read 一体操作），响应存入指定变量。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| PortName | string([ExpressionField]) | 是 | — | 已打开的端口名 |
		| WriteData | string([ExpressionField]) | 是 | — | 要发送的数据 |
		| DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |
		| ReadTimeoutMs | int | 否 | 3000 | 读取超时毫秒数 |
		| ReadBytes | int | 否 | 0 | 读取字节数，0 表示读到终止符 |
		| Terminator | string | 否 | \n | 终止符，ReadBytes=0 时生效 |
		| ResultVariable | string(变量路径) | 是 | — | 响应存入的变量名 |

		## 行为

		- 先发送 WriteData，再立即读取一次响应
		- 读取超时或端口未打开时步骤报错

		## 相关插件

		- `SerialPort_Open`：打开串口
		- `SerialPort_Write` / `SerialPort_Read`：单独的写入/读取操作
		""";

	public override IStepExecutor CreateExecutor() => new SerialPortQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.PortName} ({s.DataFormat}) -> {s.ResultVariable}";
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var serializer = CreateSerializer();
		var s = context.Setting is { Length: > 0 }
			? (SerialPortQuerySetting)serializer.Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion)
			: new SerialPortQuerySetting();

		if (string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("SP_040", "PortName 不能为空"));

		if (string.IsNullOrWhiteSpace(s.WriteData))
			errors.Add(StepSettingError.Error("SP_041", "WriteData 不能为空"));

		if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
			errors.Add(StepSettingError.Error("SP_044", "ReadTimeout 必须大于 0，或为 -1 表示永不超时"));

		if (s.ReadBytes < 0)
			errors.Add(StepSettingError.Error("SP_045", "ReadBytes 不能为负数"));

		if (s.DataFormat == SerialPortDataFormat.Hex && !string.IsNullOrWhiteSpace(s.WriteData))
		{
			var hex = s.WriteData.Trim().Replace(" ", "");
			if (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, @"^[0-9A-Fa-f]+$"))
				errors.Add(StepSettingError.Warning("SP_046", "HEX 格式数据应为偶数位十六进制字符串（如 48656C6C6F）"));
		}

		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_042", "结果变量不能为空"));
		else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_043", $"变量 {s.ResultVariable} 不存在"));
		else
		{
			var val = context.ExecutionContext.GetVariable(s.ResultVariable);
			if (val is not null && val is not string)
				errors.Add(StepSettingError.Error("SP_047", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
		}

		SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
