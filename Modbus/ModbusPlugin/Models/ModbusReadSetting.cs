using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusReadSetting
{
	/// <summary>杩炴帴鏍囪瘑鍚?/summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>浠庣珯鍦板潃</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>瀵勫瓨鍣ㄧ被鍨?/summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>璧峰鍦板潃</summary>
	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	/// <summary>璇诲彇鏁伴噺</summary>
	[ExpressionField]
	public string Quantity { get; set; } = "1";

	/// <summary>鏁版嵁鏍煎紡</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>缁撴灉淇濆瓨鍙橀噺鍚?/summary>
	[ExpressionField]
	public string ResultVariable { get; set; } = "ModbusResult";
}
