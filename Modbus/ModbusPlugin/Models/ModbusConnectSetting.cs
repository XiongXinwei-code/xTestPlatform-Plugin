using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// Modbus 连接步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class ModbusConnectSetting
{
	/// <summary>连接名称，用于在后续步骤中引用此连接</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "\"Modbus1\"";

	/// <summary>传输类型：TCP 或 RTU</summary>
	public ModbusTransportType TransportType { get; set; } = ModbusTransportType.TCP;

	/// <summary>TCP 模式下的目标 IP 地址</summary>
	[ExpressionField]
	public string IpAddress { get; set; } = "\"192.168.1.1\"";

	/// <summary>TCP 模式下的端口号（默认 502）</summary>
	public int TcpPort { get; set; } = 502;

	/// <summary>RTU 模式下的串口名称</summary>
	[ExpressionField]
	public string PortName { get; set; } = "\"COM1\"";

	/// <summary>RTU 模式下的波特率</summary>
	public int BaudRate { get; set; } = 9600;

	/// <summary>RTU 模式下的数据位</summary>
	public int DataBits { get; set; } = 8;

	/// <summary>RTU 模式下的停止位</summary>
	public int StopBits { get; set; } = 1;

	/// <summary>RTU 模式下的校验位（0=None, 1=Odd, 2=Even）</summary>
	public int Parity { get; set; } = 0;

	/// <summary>通信超时时间（毫秒）</summary>
	public int TimeoutMs { get; set; } = 3000;
}