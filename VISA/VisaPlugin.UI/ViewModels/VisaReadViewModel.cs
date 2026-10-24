using System.ComponentModel;
using System.Runtime.CompilerServices;
using VISA.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.ViewModels;

/// <summary>
/// VISA 读取编辑器 ViewModel
/// </summary>
public class VisaReadViewModel : INotifyPropertyChanged
{
	private readonly EditorSaveCoordinator _save = new();
    public Exception? LastSaveError => _save.LastError;
    public void FlushPendingSave() => _save.Flush();
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private VisaReadSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

	private void Load()
	{
        _save.Detach();
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (VisaReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (VisaReadSetting)_serializer.CreateDefault();
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        var step = _step; var setting = _setting; var serializer = _serializer;
        _save.Queue(() => step.StepSetting.Setting = serializer.Serialize(setting));
    }

	public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
	public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
	public bool TrimResponse { get => _setting?.TrimResponse ?? true; set { if (_setting == null || _setting.TrimResponse == value) return; _setting.TrimResponse = value; OnPropertyChanged(); QueueSave(); } }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
