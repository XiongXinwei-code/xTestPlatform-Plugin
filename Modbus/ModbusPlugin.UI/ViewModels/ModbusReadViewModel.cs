using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

/// <summary>
/// Modbus 读取编辑器的 ViewModel，绑定读取参数并防抖保存
/// </summary>
public class ModbusReadViewModel : INotifyPropertyChanged
{
	/// <summary>保存防抖延迟（毫秒）</summary>
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusReadSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusReadSetting)_serializer.CreateDefault();
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public void FlushPendingSave() => _saves.Flush();
    public Exception? LastSaveError => _saves.LastError;
    private void QueueSave()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(QueueSave); return; }
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        // 防抖保存留在 Dispatcher；刷新、卸载前 Flush 固定的原绑定。
        _saves.Schedule(_step, _serializer, _setting);
    }

	public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
	public int SlaveAddress { get => _setting?.SlaveAddress ?? 1; set { if (_setting == null || _setting.SlaveAddress == checked((byte)value)) return; _setting.SlaveAddress = checked((byte)value); OnPropertyChanged(); QueueSave(); } }
	public int RegisterType { get => (int)(_setting?.RegisterType ?? ModbusRegisterType.HoldingRegister); set { if (_setting == null) return; _setting.RegisterType = (ModbusRegisterType)value; OnPropertyChanged(); QueueSave(); } }
	public string StartAddress { get => _setting?.StartAddress ?? "0"; set { if (_setting == null || _setting.StartAddress == value) return; _setting.StartAddress = value; OnPropertyChanged(); QueueSave(); } }
	public string Quantity { get => _setting?.Quantity ?? "1"; set { if (_setting == null || _setting.Quantity == value) return; _setting.Quantity = value; OnPropertyChanged(); QueueSave(); } }
	public int DataFormat { get => (int)(_setting?.DataFormat ?? ModbusDataFormat.UInt16); set { if (_setting == null) return; _setting.DataFormat = (ModbusDataFormat)value; OnPropertyChanged(); QueueSave(); } }
	public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
