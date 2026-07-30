using LXI.Models;

namespace LXI.UI.ViewModels;

public class LxiOpenViewModel : LxiViewModelBase
{
	private LxiOpenSetting? _setting;

	protected override void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (LxiOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (LxiOpenSetting)_serializer.CreateDefault();
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	protected override object? GetSetting() => _setting;

	public string IpAddress
	{
		get => _setting?.IpAddress ?? string.Empty;
		set { if (_setting != null && _setting.IpAddress != value) { _setting.IpAddress = value; OnPropertyChanged(); QueueSave(); } }
	}

	public int Port
	{
		get => _setting?.Port ?? 5025;
		set { if (_setting != null && _setting.Port != value) { _setting.Port = value; OnPropertyChanged(); QueueSave(); } }
	}

	public int ConnectTimeoutMs
	{
		get => _setting?.ConnectTimeoutMs ?? 5000;
		set { if (_setting != null && _setting.ConnectTimeoutMs != value) { _setting.ConnectTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
	}

	public string Terminator
	{
		get => _setting?.Terminator ?? "\n";
		set { if (_setting != null && _setting.Terminator != value) { _setting.Terminator = value; OnPropertyChanged(); QueueSave(); } }
	}
}