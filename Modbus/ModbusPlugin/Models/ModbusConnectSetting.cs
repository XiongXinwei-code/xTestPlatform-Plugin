using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusConnectSetting
{
	/// <summary>杩炴帴鏍囪瘑鍚?/summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>浼犺緭绫诲瀷 TCP/RTU</summary>
	public ModbusTransportType TransportType { get; set; } = ModbusTransportType.TCP;

	// --- TCP ---
	/// <summary>IP 鍦板潃</summary>
	[ExpressionField]
	public string IpAddress { get; set; } = "192.168.1.1";

	/// <summary>TCP 绔彛</summary>
	public int TcpPort { get; set; } = 502;

	// --- RTU ---
	/// <summary>涓插彛鍚嶇О</summary>
	[ExpressionField]
	public string PortName { get; set; } = "COM1";

	/// <summary>娉㈢壒鐜?/summary>
	public int BaudRate { get; set; } = 9600;

	/// <summary>鏁版嵁浣?/summary>
	public int DataBits { get; set; } = 8;

	/// <summary>鍋滄浣?(1=One, 2=Two)</summary>
	public int StopBits { get; set; } = 1;

	/// <summary>鏍￠獙 (0=None, 1=Odd, 2=Even)</summary>
	public int Parity { get; set; } = 0;

	/// <summary>瓒呮椂(ms)</summary>
	public int TimeoutMs { get; set; } = 3000;
}
