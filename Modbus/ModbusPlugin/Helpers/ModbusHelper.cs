namespace Modbus.Helpers;

public static class ModbusHelper
{
	public static string GetConnectionKey(string connectionName) => $"__Modbus_{connectionName}";
}
