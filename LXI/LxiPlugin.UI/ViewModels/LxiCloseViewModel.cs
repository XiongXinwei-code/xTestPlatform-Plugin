using LXI.Models;

namespace LXI.UI.ViewModels;

public class LxiCloseViewModel : LxiViewModelBase
{
	private LxiCloseSetting? _setting;

	protected override void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (LxiCloseSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (LxiCloseSetting)_serializer.CreateDefault();
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
}