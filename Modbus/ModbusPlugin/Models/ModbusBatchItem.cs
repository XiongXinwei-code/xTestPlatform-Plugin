using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchItem
{
	/// <summary>浠庣珯鍦板潃</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>瀵勫瓨鍣ㄧ被鍨?/summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>璧峰鍦板潃</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>鏁伴噺</summary>
	public ushort Quantity { get; set; } = 1;

	/// <summary>鏁版嵁鏍煎紡</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>缁撴灉鍙橀噺鍚?/summary>
	public string ResultVariable { get; set; } = "";
}
