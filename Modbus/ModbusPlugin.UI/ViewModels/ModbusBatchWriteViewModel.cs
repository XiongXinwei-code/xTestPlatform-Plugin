using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

/// <summary>
/// Modbus 批量写入编辑器的 ViewModel，管理批量写入项列表
/// </summary>
public class ModbusBatchWriteViewModel : INotifyPropertyChanged
{
	/// <summary>保存防抖延迟（毫秒）</summary>
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusBatchWriteSetting? _setting;

	public ObservableCollection<ModbusBatchWriteItem> Items { get; private set; } = new();

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusBatchWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusBatchWriteSetting)_serializer.CreateDefault();
			Items = _setting.Items;
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
	public int IntervalMs { get => _setting?.IntervalMs ?? 0; set { if (_setting == null || _setting.IntervalMs == value) return; _setting.IntervalMs = value; OnPropertyChanged(); QueueSave(); } }

	public void AddItem() { Items.Add(new ModbusBatchWriteItem()); QueueSave(); }
	public void RemoveItem(ModbusBatchWriteItem item) { Items.Remove(item); QueueSave(); }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}