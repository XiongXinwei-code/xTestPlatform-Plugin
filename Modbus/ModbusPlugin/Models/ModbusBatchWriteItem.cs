using MessagePack;

namespace Modbus.Models;

/// <summary>
/// 批量写入中的单个写入项配置
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchWriteItem
{
	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器/线圈类型</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>要写入的值，多个值用逗号分隔</summary>
	public string Values { get; set; } = "0";

	/// <summary>数据解析格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}