using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.ViewModels;

public sealed class ReadModeViewModel : INotifyPropertyChanged
{
	private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
	private IStepSettingSerializer? _serializer;
	private Step? _step;
	private ReadModeSetting? _setting;
	private bool _dirty;
	private bool _saveFailed;
	private readonly PluginCommon.EditorSaveScheduler _pendingSave = new();
	public string Error { get; private set; } = "";
	public Action<string, Action>? ExecuteCommand { get; set; }

	public ReadModeViewModel() => _saveTimer.Tick += (_, _) => Flush();
	public void AttachSerializer(IStepSettingSerializer serializer) { Flush(); EnsureSaved(); _serializer = serializer; }
	public void AttachStep(Step step)
	{
		Flush();
		EnsureSaved();
		_step = step;
		_dirty = false;
		try
		{
			_setting = _serializer == null ? null : step.StepSetting.Setting is { Length: > 0 } data
				? (ReadModeSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
				: (ReadModeSetting)_serializer.CreateDefault();
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
		_saveFailed = false;
		try
		{
			if (_dirty && _step != null && _serializer != null && _setting != null)
			{
				var bytes = _serializer.Serialize(_setting);
				var step = _step;
				var version = _serializer.SettingVersion;
				_dirty = false;
				_pendingSave.Queue(() =>
				{
					if (bytes.SequenceEqual(step.StepSetting.Setting ?? [])) return;
					void Save() { step.StepSetting.Setting = bytes; step.StepSetting.SettingVersion = version; }
					if (ExecuteCommand != null) ExecuteCommand("修改 CiA402 参数", Save); else Save();
				});
			}
			_pendingSave.Flush();
			Error = _setting == null ? "设置不存在" : string.Join("；", SettingValidation.Validate(_setting));
			OnPropertyChanged(nameof(Error));
		}
		catch (Exception ex) { _saveFailed = true; Error = $"保存设置失败：{ex.Message}"; OnPropertyChanged(nameof(Error)); }
	}

	private void EnsureSaved()
	{
		if (_saveFailed) throw new InvalidOperationException("上一步配置保存失败，已保留待重试内容；请先重试保存再切换步骤", _pendingSave.LastError);
	}

    public string ModeVariable
    {
        get => _setting?.ModeVariable ?? "";
        set { if (_setting == null || _setting.ModeVariable == value) return; _setting.ModeVariable = value; OnPropertyChanged(); QueueSave(); }
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
