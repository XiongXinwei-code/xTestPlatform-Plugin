using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusWriteSetting
{
	/// <summary>杩炴帴鏍囪瘑鍚?/summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>浠庣珯鍦板潃</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>瀵勫瓨鍣ㄧ被鍨?(Coil / HoldingRegister)</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>璧峰鍦板潃</summary>
	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	/// <summary>瑕佸啓鍏ョ殑鍊硷紙閫楀彿鍒嗛殧锛屽 "100,200,300"锛?/summary>
	[ExpressionField]
	public string Values { get; set; } = "0";

	/// <summary>鏁版嵁鏍煎紡</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
