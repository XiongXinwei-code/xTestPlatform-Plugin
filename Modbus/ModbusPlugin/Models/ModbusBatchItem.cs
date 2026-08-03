using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// 批量读取中的单个读取项配置
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchItem
{
	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器/线圈类型</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>读取数量</summary>
	public ushort Quantity { get; set; } = 1;

	/// <summary>数据解析格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>存储读取结果的变量名（为空则不存储）</summary>
	public string ResultVariable { get; set; } = "";
}