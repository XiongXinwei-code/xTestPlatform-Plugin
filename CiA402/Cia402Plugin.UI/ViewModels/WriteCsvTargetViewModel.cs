using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.ViewModels;

public sealed class WriteCsvTargetViewModel : INotifyPropertyChanged
{
	private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
	private IStepSettingSerializer? _serializer;
	private Step? _step;
	private WriteCsvTargetSetting? _setting;
	private bool _dirty;
	public string Error { get; private set; } = "";
	public Action<string, Action>? ExecuteCommand { get; set; }

	public WriteCsvTargetViewModel() => _saveTimer.Tick += (_, _) => Flush();
	public void AttachSerializer(IStepSettingSerializer serializer) { Flush(); _serializer = serializer; }
	public void AttachStep(Step step)
	{
		Flush();
		_step = step;
		_dirty = false;
		try
		{
			_setting = _serializer == null ? null : step.StepSetting.Setting is { Length: > 0 } data
				? (WriteCsvTargetSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
				: (WriteCsvTargetSetting)_serializer.CreateDefault();
			Error = _setting == null ? "设置不存在" : string.Join("；", SettingValidation.Validate(_setting));
		}
		catch (Exception ex) { _setting = null; Error = $"读取设置失败：{ex.Message}"; }
		OnPropertyChanged(string.Empty);
	}

	private void QueueSave()
	{
		_dirty = true;
		Error = _setting == null ? "设置不存在" : string.Join("；", SettingValidation.Validate(_setting));
		OnPropertyChanged(nameof(Error));
		_saveTimer.Stop();
		_saveTimer.Start();
	}

	public void Flush()
	{
		_saveTimer.Stop();
		if (!_dirty || _step == null || _serializer == null || _setting == null) return;
		try
		{
			var bytes = _serializer.Serialize(_setting);
			_dirty = false;
			if (bytes.SequenceEqual(_step.StepSetting.Setting ?? [])) return;
			var step = _step;
			void Save() { step.StepSetting.Setting = bytes; step.StepSetting.SettingVersion = 1; }
			if (ExecuteCommand != null) ExecuteCommand("修改 CiA402 参数", Save); else Save();
		}
		catch (Exception ex) { Error = $"保存设置失败：{ex.Message}"; OnPropertyChanged(nameof(Error)); }
	}

    public string TargetVelocity
    {
        get => _setting?.TargetVelocity ?? "";
        set { if (_setting == null || _setting.TargetVelocity == value) return; _setting.TargetVelocity = value; OnPropertyChanged(); QueueSave(); }
    }

    public string AxisName
    {
        get => _setting?.AxisName ?? "";
        set { if (_setting == null || _setting.AxisName == value) return; _setting.AxisName = value; OnPropertyChanged(); QueueSave(); }
    }

    public int TimeoutMs
    {
        get => _setting?.TimeoutMs ?? default;
        set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public int PollIntervalMs
    {
        get => _setting?.PollIntervalMs ?? default;
        set { if (_setting == null || _setting.PollIntervalMs == value) return; _setting.PollIntervalMs = value; OnPropertyChanged(); QueueSave(); }
    }

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
