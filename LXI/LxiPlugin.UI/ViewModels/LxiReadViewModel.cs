using LXI.Models;

namespace LXI.UI.ViewModels;

public class LxiReadViewModel : LxiViewModelBase
{
	private LxiReadSetting? _setting;

	protected override void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (LxiReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (LxiReadSetting)_serializer.CreateDefault();
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

	public int ReadTimeoutMs
	{
		get => _setting?.ReadTimeoutMs ?? 5000;
		set { if (_setting != null && _setting.ReadTimeoutMs != value) { _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
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