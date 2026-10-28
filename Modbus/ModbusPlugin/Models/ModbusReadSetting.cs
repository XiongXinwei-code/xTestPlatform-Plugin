using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// Modbus 读取步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class ModbusReadSetting
{
	/// <summary>使用的连接名称</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "\"Modbus1\"";

	/// <summary>从站地址（1~247）</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>要读取的寄存器/线圈类型</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址（支持表达式）</summary>
	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	/// <summary>读取数量：位 1～2000，寄存器 1～125；32 位格式须为偶数（支持表达式）</summary>
	[ExpressionField]
	public string Quantity { get; set; } = "1";

	/// <summary>数据解析格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>必填：已声明的完整变量路径；已知值类型和标量/数组形状须匹配</summary>
	[VariablePathField]
	public string ResultVariable { get; set; } = "";
}
