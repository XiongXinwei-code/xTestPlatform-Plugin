using Modbus.Models;

namespace Modbus.Helpers;

/// <summary>
/// Modbus 寄存器数据格式转换工具，支持多种字节序和数据类型
/// </summary>
public static class ModbusDataConverter
{
	/// <summary>
	/// 将读取到的原始寄存器值按指定格式解析为对应的数据类型
	/// </summary>
	public static object ConvertRegisters(ushort[] registers, ModbusDataFormat format)
	{
		if (registers.Length == 0) return Array.Empty<ushort>();

		return format switch
		{
			ModbusDataFormat.UInt16 => registers.Length == 1 ? registers[0] : registers,
			ModbusDataFormat.Int16 => registers.Length == 1
				? (object)(short)registers[0]
				: registers.Select(r => (short)r).ToArray(),
			ModbusDataFormat.UInt32_AB_CD => ConvertPairs(registers, (a, b) => (uint)((a << 16) | b)),
			ModbusDataFormat.Int32_AB_CD => ConvertPairs(registers, (a, b) => (int)((a << 16) | b)),
			ModbusDataFormat.Float_AB_CD => ConvertPairs(registers, (a, b) =>
			{
				var bytes = BitConverter.GetBytes((uint)((a << 16) | b));
				return BitConverter.ToSingle(bytes, 0);
			}),
			ModbusDataFormat.UInt32_CD_AB => ConvertPairs(registers, (a, b) => (uint)((b << 16) | a)),
			ModbusDataFormat.Int32_CD_AB => ConvertPairs(registers, (a, b) => (int)((b << 16) | a)),
			ModbusDataFormat.Float_CD_AB => ConvertPairs(registers, (a, b) =>
			{
				var bytes = BitConverter.GetBytes((uint)((b << 16) | a));
				return BitConverter.ToSingle(bytes, 0);
			}),
			_ => registers
		};
	}

	/// <summary>
	/// 将寄存器数组按两两一组进行转换（用于 32 位数据类型）
	/// </summary>
	private static object ConvertPairs<T>(ushort[] registers, Func<ushort, ushort, T> converter)
	{
		var results = new List<T>();
		for (int i = 0; i + 1 < registers.Length; i += 2)
			results.Add(converter(registers[i], registers[i + 1]));
		if (results.Count == 1) return results[0]!;
		return results.ToArray();
	}

	/// <summary>
	/// 将逗号分隔的字符串值按指定格式转换为 ushort 寄存器数组（用于写入操作）
	/// </summary>
	public static ushort[] ConvertToRegisters(string valuesStr, ModbusDataFormat format)
	{
		var parts = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		return format switch
		{
			ModbusDataFormat.UInt16 => parts.Select(p => ushort.Parse(p)).ToArray(),
			ModbusDataFormat.Int16 => parts.Select(p => (ushort)(short.Parse(p))).ToArray(),
			ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.Int32_AB_CD =>
				parts.SelectMany(p => { var v = uint.Parse(p); return new ushort[] { (ushort)(v >> 16), (ushort)(v & 0xFFFF) }; }).ToArray(),
			ModbusDataFormat.Float_AB_CD =>
				parts.SelectMany(p => { var v = BitConverter.ToUInt32(BitConverter.GetBytes(float.Parse(p)), 0); return new ushort[] { (ushort)(v >> 16), (ushort)(v & 0xFFFF) }; }).ToArray(),
			ModbusDataFormat.UInt32_CD_AB or ModbusDataFormat.Int32_CD_AB =>
				parts.SelectMany(p => { var v = uint.Parse(p); return new ushort[] { (ushort)(v & 0xFFFF), (ushort)(v >> 16) }; }).ToArray(),
			ModbusDataFormat.Float_CD_AB =>
				parts.SelectMany(p => { var v = BitConverter.ToUInt32(BitConverter.GetBytes(float.Parse(p)), 0); return new ushort[] { (ushort)(v & 0xFFFF), (ushort)(v >> 16) }; }).ToArray(),
			_ => parts.Select(p => ushort.Parse(p)).ToArray()
		};
	}
}