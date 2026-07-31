namespace Modbus.Helpers;

/// <summary>
/// Modbus 通用辅助方法
/// </summary>
public static class ModbusHelper
{
	/// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
	public static string GetConnectionKey(string connectionName) => $"__Modbus_{connectionName}";
}