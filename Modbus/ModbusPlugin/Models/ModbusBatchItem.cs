using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// 批量读取中的单个读取项配置
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchItem : INotifyPropertyChanged
{
	private byte _slaveAddress = 1;
	private ModbusRegisterType _registerType = ModbusRegisterType.HoldingRegister;
	private ushort _startAddress = 0;
	private ushort _quantity = 1;
	private ModbusDataFormat _dataFormat = ModbusDataFormat.UInt16;
	private string _resultVariable = "";

	/// <summary>从站地址</summary>
	public byte SlaveAddress { get => _slaveAddress; set => SetProperty(ref _slaveAddress, value); }

	/// <summary>寄存器/线圈类型</summary>
	public ModbusRegisterType RegisterType { get => _registerType; set => SetProperty(ref _registerType, value); }

	/// <summary>起始地址</summary>
	public ushort StartAddress { get => _startAddress; set => SetProperty(ref _startAddress, value); }

	/// <summary>读取数量</summary>
	public ushort Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }

	/// <summary>数据解析格式</summary>
	public ModbusDataFormat DataFormat { get => _dataFormat; set => SetProperty(ref _dataFormat, value); }

	/// <summary>存储读取结果的变量路径（为空则不存储）</summary>
	[VariablePathField]
	public string ResultVariable { get => _resultVariable; set => SetProperty(ref _resultVariable, value); }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
		storage = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}