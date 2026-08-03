using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

/// <summary>
/// Modbus 连接编辑器的 ViewModel，绑定连接参数并通过防抖保存序列化到 Step
/// </summary>
public class ModbusConnectViewModel : INotifyPropertyChanged
{
	/// <summary>保存防抖延迟（毫秒）</summary>
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusConnectSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusConnectSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusConnectSetting)_serializer.CreateDefault();
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
	public int TransportType { get => (int)(_setting?.TransportType ?? ModbusTransportType.TCP); set { if (_setting == null) return; _setting.TransportType = (ModbusTransportType)value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTcp)); OnPropertyChanged(nameof(IsRtu)); QueueSave(); } }
	public string IpAddress { get => _setting?.IpAddress ?? ""; set { if (_setting == null || _setting.IpAddress == value) return; _setting.IpAddress = value; OnPropertyChanged(); QueueSave(); } }
	public int TcpPort { get => _setting?.TcpPort ?? 502; set { if (_setting == null || _setting.TcpPort == value) return; _setting.TcpPort = value; OnPropertyChanged(); QueueSave(); } }
	public string PortName { get => _setting?.PortName ?? ""; set { if (_setting == null || _setting.PortName == value) return; _setting.PortName = value; OnPropertyChanged(); QueueSave(); } }
	public int BaudRate { get => _setting?.BaudRate ?? 9600; set { if (_setting == null || _setting.BaudRate == value) return; _setting.BaudRate = value; OnPropertyChanged(); QueueSave(); } }
	public int DataBits { get => _setting?.DataBits ?? 8; set { if (_setting == null || _setting.DataBits == value) return; _setting.DataBits = value; OnPropertyChanged(); QueueSave(); } }
	public int StopBits { get => _setting?.StopBits ?? 1; set { if (_setting == null || _setting.StopBits == value) return; _setting.StopBits = value; OnPropertyChanged(); QueueSave(); } }
	public int Parity { get => _setting?.Parity ?? 0; set { if (_setting == null || _setting.Parity == value) return; _setting.Parity = value; OnPropertyChanged(); QueueSave(); } }
	public int TimeoutMs { get => _setting?.TimeoutMs ?? 3000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

	public bool IsTcp => (_setting?.TransportType ?? ModbusTransportType.TCP) == ModbusTransportType.TCP;
	public bool IsRtu => (_setting?.TransportType ?? ModbusTransportType.TCP) == ModbusTransportType.RTU;

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}