using LXI.Models;

namespace LXI.UI.ViewModels;

public class LxiWriteViewModel : LxiViewModelBase
{
	private LxiWriteSetting? _setting;

	protected override void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (LxiWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (LxiWriteSetting)_serializer.CreateDefault();
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

	public string Command
	{
		get => _setting?.Command ?? string.Empty;
		set { if (_setting != null && _setting.Command != value) { _setting.Command = value; OnPropertyChanged(); QueueSave(); } }
	}

	public string Terminator
	{
		get => _setting?.Terminator ?? "\n";
		set { if (_setting != null && _setting.Terminator != value) { _setting.Terminator = value; OnPropertyChanged(); QueueSave(); } }
	}
}