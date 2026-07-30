using SerialPort.Models;

namespace SerialPort.UI.ViewModels;

public sealed class SerialPortQueryViewModel : SerialPortViewModelBase
{
	private SerialPortQuerySetting? _setting;

	protected override void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (SerialPortQuerySetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (SerialPortQuerySetting)_serializer.CreateDefault();
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	protected override object? GetSetting() => _setting;

	public string PortName
	{
		get => _setting?.PortName ?? string.Empty;
		set { if (_setting != null && _setting.PortName != value) { _setting.PortName = value; OnPropertyChanged(); QueueSave(); } }
	}

	public string WriteData
	{
		get => _setting?.WriteData ?? string.Empty;
		set { if (_setting != null && _setting.WriteData != value) { _setting.WriteData = value; OnPropertyChanged(); QueueSave(); } }
	}

	public SerialPortDataFormat DataFormat
	{
		get => _setting?.DataFormat ?? SerialPortDataFormat.String;
		set { if (_setting != null && _setting.DataFormat != value) { _setting.DataFormat = value; OnPropertyChanged(); QueueSave(); } }
	}

	public int ReadTimeoutMs
	{
		get => _setting?.ReadTimeoutMs ?? 3000;
		set { if (_setting != null && _setting.ReadTimeoutMs != value) { _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
	}

	public int ReadBytes
	{
		get => _setting?.ReadBytes ?? 0;
		set { if (_setting != null && _setting.ReadBytes != value) { _setting.ReadBytes = value; OnPropertyChanged(); QueueSave(); } }
	}

	public string Terminator
	{
		get => _setting?.Terminator ?? "\n";
		set { if (_setting != null && _setting.Terminator != value) { _setting.Terminator = value; OnPropertyChanged(); QueueSave(); } }
	}

	public string ResultVariable
	{
		get => _setting?.ResultVariable ?? string.Empty;
		set { if (_setting != null && _setting.ResultVariable != value) { _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
	}
}