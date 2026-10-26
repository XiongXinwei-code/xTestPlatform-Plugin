using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 连接插件，支持 TCP 和 RTU(串口) 两种传输方式
/// </summary>
public sealed class ModbusConnectPlugin : StepPluginBase<ModbusConnectSetting>
{
	public override string StepTypeId => "IO.ModbusConnect";
	public override string DisplayName => "Modbus_Connect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus_connect.png";

	public override string Description => """
		## 功能

		建立 Modbus 连接，支持 TCP 和 RTU（串口）两种传输方式，连接成功后通过 ConnectionName 标识连接。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | Modbus1 | 连接标识名，序列内唯一，求值结果为 string |
		| TransportType | 枚举 | 是 | TCP | 可选值：TCP, RTU |
		| IpAddress | string([ExpressionField]) | TCP 时 | 192.168.1.1 | TCP 服务器地址，求值结果为 string |
		| TcpPort | int | 否 | 502 | TCP 端口 |
		| PortName | string([ExpressionField]) | RTU 时 | — | 串口名，如 COM1，求值结果为 string |
		| BaudRate | int | 否 | 9600 | 波特率（RTU） |
		| DataBits | int | 否 | 8 | 数据位（RTU） |
		| StopBits | int | 否 | 1 | 停止位：1=One, 2=Two, 3=OnePointFive（RTU） |
		| Parity | int | 否 | 0 | 校验位：0=None, 1=Odd, 2=Even, 3=Mark, 4=Space（RTU） |
		| TimeoutMs | int | 否 | 3000 | 建立总期限（含旧收尾等待），后续每次通信也复用此期限 |

		## 行为

		- TransportType=TCP 时使用 IpAddress/TcpPort，RTU 时使用串口参数
		- 连接失败时步骤报错
		- 连接 owner 以 `ConnectionName` 为逻辑标识统一持有主站、传输对象与超时配置，供后续读写/断开步骤使用
		- 同名活动连接拒绝覆盖；已请求收尾或已确认释放的旧代次可在 TimeoutMs 同一总期限内接续关闭后重开，不强清未退出调用，不关闭其他活动连接
		- 仅支持 TCP 与 RTU 两种传输方式，不支持 Modbus ASCII

		### 检索关键词

		Modbus、Modbus TCP、Modbus RTU、模总线、
		从站地址、从机地址、Slave ID、Unit ID、功能码、
		RS-485、RS485、串口从站、PLC 通信、仪表采集

		寄存器与线圈（保持寄存器、输入寄存器、线圈、离散输入）的选择在 `Modbus_Read` / `Modbus_Write` 步骤中配置，不在本步骤。

		## 相关插件

		- `Modbus_Read` / `Modbus_Write` / `Modbus_BatchRead` / `Modbus_BatchWrite`：在此连接上读写数据
		- `Modbus_Disconnect`：关闭本插件建立的连接
		""";

	public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Modbus", () => new ModbusConnectExecutor());

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}
