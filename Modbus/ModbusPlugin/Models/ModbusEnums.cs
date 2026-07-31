namespace Modbus.Models;

public enum ModbusTransportType
{
	TCP = 0,
	RTU = 1
}

public enum ModbusRegisterType
{
	Coil = 0,
	DiscreteInput = 1,
	HoldingRegister = 2,
	InputRegister = 3
}

public enum ModbusDataFormat
{
	UInt16 = 0,
	Int16 = 1,
	UInt32_AB_CD = 2,
	Int32_AB_CD = 3,
	Float_AB_CD = 4,
	UInt32_CD_AB = 5,
	Int32_CD_AB = 6,
	Float_CD_AB = 7
}
