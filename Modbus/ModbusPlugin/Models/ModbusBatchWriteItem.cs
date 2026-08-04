using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace Modbus.Models;

/// <summary>
/// 批量写入中的单个写入项配置
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchWriteItem : INotifyPropertyChanged
{
	private byte _slaveAddress = 1;
	private ModbusRegisterType _registerType = ModbusRegisterType.HoldingRegister;
	private ushort _startAddress = 0;
	private string _values = "0";
	private ModbusDataFormat _dataFormat = ModbusDataFormat.UInt16;

	/// <summary>从站地址</summary>
	public byte SlaveAddress { get => _slaveAddress; set => SetProperty(ref _slaveAddress, value); }

	/// <summary>寄存器/线圈类型</summary>
	public ModbusRegisterType RegisterType { get => _registerType; set => SetProperty(ref _registerType, value); }

	/// <summary>起始地址</summary>
	public ushort StartAddress { get => _startAddress; set => SetProperty(ref _startAddress, value); }

	/// <summary>要写入的值，多个值用逗号分隔</summary>
	public string Values { get => _values; set => SetProperty(ref _values, value); }

	/// <summary>数据解析格式</summary>
	public ModbusDataFormat DataFormat { get => _dataFormat; set => SetProperty(ref _dataFormat, value); }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
		storage = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}