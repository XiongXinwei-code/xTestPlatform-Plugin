namespace Modbus.Models;

/// <summary>
/// Modbus 传输类型（TCP 或 RTU 串口）
/// </summary>
public enum ModbusTransportType
{
	/// <summary>TCP/IP 连接</summary>
	TCP = 0,
	/// <summary>RTU 串口连接</summary>
	RTU = 1
}

/// <summary>
/// Modbus 寄存器/线圈类型
/// </summary>
public enum ModbusRegisterType
{
	/// <summary>线圈（读写，功能码 01/05/15）</summary>
	Coil = 0,
	/// <summary>离散输入（只读，功能码 02）</summary>
	DiscreteInput = 1,
	/// <summary>保持寄存器（读写，功能码 03/06/16）</summary>
	HoldingRegister = 2,
	/// <summary>输入寄存器（只读，功能码 04）</summary>
	InputRegister = 3
}

/// <summary>
/// 寄存器数据解析格式（字节序与数据类型）
/// </summary>
public enum ModbusDataFormat
{
	/// <summary>无符号 16 位整数（1 个寄存器）</summary>
	UInt16 = 0,
	/// <summary>有符号 16 位整数（1 个寄存器）</summary>
	Int16 = 1,
	/// <summary>无符号 32 位整数，大端字节序 AB_CD（2 个寄存器）</summary>
	UInt32_AB_CD = 2,
	/// <summary>有符号 32 位整数，大端字节序 AB_CD（2 个寄存器）</summary>
	Int32_AB_CD = 3,
	/// <summary>32 位浮点数，大端字节序 AB_CD（2 个寄存器）</summary>
	Float_AB_CD = 4,
	/// <summary>无符号 32 位整数，小端字节序 CD_AB（2 个寄存器）</summary>
	UInt32_CD_AB = 5,
	/// <summary>有符号 32 位整数，小端字节序 CD_AB（2 个寄存器）</summary>
	Int32_CD_AB = 6,
	/// <summary>32 位浮点数，小端字节序 CD_AB（2 个寄存器）</summary>
	Float_CD_AB = 7
}