using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortQueryPlugin : StepPluginBase<SerialPortQuerySetting>
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
		| ResultVariable | string([ExpressionField]) | 是 | — | 响应存入的变量名 |

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
}