using MessagePack;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteItem
{
	/// <summary>浠庣珯鍦板潃</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>瀵勫瓨鍣ㄧ被鍨?(Coil/HoldingRegister)</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>璧峰鍦板潃</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>鍐欏叆鍊?閫楀彿鍒嗛殧)</summary>
	public string Values { get; set; } = "0";

	/// <summary>鏁版嵁鏍煎紡</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
