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
	public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport_query.png";

	public override string Description => """
		## 功能

		向已打开的串口发送数据并读取响应（Write+Read 一体操作），响应存入指定变量。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| PortName | string([ExpressionField]) | 是 | — | 已打开的端口名，求值结果为 string |
		| WriteData | string([ExpressionField]) | 是 | — | 要发送的数据，求值结果为 string |
		| DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |
		| ReadTimeoutMs | int | 否 | 3000 | 读取超时毫秒数 |
		| ReadBytes | int | 否 | 0 | 读取字节数，0 表示读到终止符 |
		| Terminator | string | 否 | \n | 终止符，ReadBytes=0 时生效 |
		| ResultVariable | string(VariablePathField) | 否 | 空 | 已声明的 string 目标完整路径，如 Locals.result；留空只返回结果，不写变量 |

		## 行为

		- 先发送 WriteData，再立即读取一次响应
		- 读取超时或端口未打开时步骤报错
		- 非空 ResultVariable 在发送查询前检查作用域、变量存在及 string 类型；不隐式创建变量，不因输出配置错误先发送设备请求
		- ReadTimeoutMs 是写入后的读取窗口；有限配置时，排队、写入至返回的外层上界为 Open.WriteTimeoutMs+ReadTimeoutMs+1000 ms。额外 1000 ms 是兼容收尾余量，不能当作已撤回发送；任一读写阶段配置 -1 时外层为无限等待，原调用方取消仍有效
		- ReadBytes=0 且 Terminator 为空时，在读取窗口到期后返回已收数据；无限窗口必须指定字节数或终止符。取消后未退出调用及部分响应不交给下一代连接复用

		## 相关插件

		- `SerialPort_Open`：打开串口
		- `SerialPort_Write` / `SerialPort_Read`：单独的写入/读取操作
		""";

	public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("SerialPort", () => new SerialPortQueryExecutor());

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.PortName} ({s.DataFormat}) -> {s.ResultVariable}";
	}
}
