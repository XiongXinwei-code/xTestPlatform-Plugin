using Modbus.Models;

namespace Modbus.Helpers;

public static class ModbusDataConverter
{
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

	private static object ConvertPairs<T>(ushort[] registers, Func<ushort, ushort, T> converter)
	{
		var results = new List<T>();
		for (int i = 0; i + 1 < registers.Length; i += 2)
			results.Add(converter(registers[i], registers[i + 1]));
		if (results.Count == 1) return results[0]!;
		return results.ToArray();
	}

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
