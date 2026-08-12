using System.ComponentModel;
using System.Runtime.CompilerServices;
using VISA.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.ViewModels;

/// <summary>
/// VISA 打开会话编辑器 ViewModel
/// </summary>
public class VisaOpenViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private VisaOpenSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (VisaOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (VisaOpenSetting)_serializer.CreateDefault();
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	private void QueueSave()
	{
		if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
		_saveCts?.Cancel();
		var cts = _saveCts = new CancellationTokenSource();
		_ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
	}

	public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
	public string ResourceString { get => _setting?.ResourceString ?? ""; set { if (_setting == null || _setting.ResourceString == value) return; _setting.ResourceString = value; OnPropertyChanged(); QueueSave(); } }
	public int OpenTimeoutMs { get => _setting?.OpenTimeoutMs ?? 5000; set { if (_setting == null || _setting.OpenTimeoutMs == value) return; _setting.OpenTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
	public int IoTimeoutMs { get => _setting?.IoTimeoutMs ?? 10000; set { if (_setting == null || _setting.IoTimeoutMs == value) return; _setting.IoTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
	public string Terminator { get => EscapeTerminator(_setting?.Terminator); set { if (_setting == null || Terminator == value) return; _setting.Terminator = value; OnPropertyChanged(); QueueSave(); } }

	/// <summary>常用终止符预设（转义文本形式），供下拉选择，也可手动输入自定义值</summary>
	public IReadOnlyList<string> TerminatorPresets { get; } = new[] { "\\n", "\\r\\n", "\\r" };

	/// <summary>将真实控制字符转义为可见文本（兼容旧数据中存储的真实 \r\n 字符），保证单行文本框可见</summary>
	private static string EscapeTerminator(string? terminator)
	{
		if (string.IsNullOrEmpty(terminator)) return "\\n";
		return terminator.Replace("\r", "\\r").Replace("\n", "\\n");
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}