using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.ViewModels;

public sealed class BindAxisViewModel : INotifyPropertyChanged
{
	private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
	private IStepSettingSerializer? _serializer;
	private Step? _step;
	private BindAxisSetting? _setting;
	private bool _dirty;
	public string Error { get; private set; } = "";
	public Action<string, Action>? ExecuteCommand { get; set; }

	public BindAxisViewModel() => _saveTimer.Tick += (_, _) => Flush();
	public void AttachSerializer(IStepSettingSerializer serializer) { Flush(); _serializer = serializer; }
	public void AttachStep(Step step)
	{
		Flush();
		_step = step;
		_dirty = false;
		try
		{
			_setting = _serializer == null ? null : step.StepSetting.Setting is { Length: > 0 } data
				? (BindAxisSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
				: (BindAxisSetting)_serializer.CreateDefault();
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

    public TransportKind Transport
    {
        get => _setting?.Transport ?? default;
        set { if (_setting == null || _setting.Transport == value) return; _setting.Transport = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCANopen)); OnPropertyChanged(nameof(IsEtherCAT)); QueueSave(); }
    }

    public Array TransportOptions { get; } = Enum.GetValues<TransportKind>();

    public string ConnectionName
    {
        get => _setting?.ConnectionName ?? "";
        set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }

    public string NodeId
    {
        get => _setting?.NodeId ?? "";
        set { if (_setting == null || _setting.NodeId == value) return; _setting.NodeId = value; OnPropertyChanged(); QueueSave(); }
    }

    public string SlaveAddress
    {
        get => _setting?.SlaveAddress ?? "";
        set { if (_setting == null || _setting.SlaveAddress == value) return; _setting.SlaveAddress = value; OnPropertyChanged(); QueueSave(); }
    }

    public int ObjectIndexOffset
    {
        get => _setting?.ObjectIndexOffset ?? default;
        set { if (_setting == null || _setting.ObjectIndexOffset == value) return; _setting.ObjectIndexOffset = value; OnPropertyChanged(); QueueSave(); }
    }

    public int CommunicationTimeoutMs
    {
        get => _setting?.CommunicationTimeoutMs ?? default;
        set { if (_setting == null || _setting.CommunicationTimeoutMs == value) return; _setting.CommunicationTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public int FeedbackMaxAgeMs
    {
        get => _setting?.FeedbackMaxAgeMs ?? default;
        set { if (_setting == null || _setting.FeedbackMaxAgeMs == value) return; _setting.FeedbackMaxAgeMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public int CspPdoNumber
    {
        get => _setting?.CspPdoNumber ?? default;
        set { if (_setting == null || _setting.CspPdoNumber == value) return; _setting.CspPdoNumber = value; OnPropertyChanged(); QueueSave(); }
    }

    public string CspPdoLayout
    {
        get => _setting?.CspPdoLayout ?? "";
        set { if (_setting == null || _setting.CspPdoLayout == value) return; _setting.CspPdoLayout = value; OnPropertyChanged(); QueueSave(); }
    }

    public int CsvPdoNumber
    {
        get => _setting?.CsvPdoNumber ?? default;
        set { if (_setting == null || _setting.CsvPdoNumber == value) return; _setting.CsvPdoNumber = value; OnPropertyChanged(); QueueSave(); }
    }

    public string CsvPdoLayout
    {
        get => _setting?.CsvPdoLayout ?? "";
        set { if (_setting == null || _setting.CsvPdoLayout == value) return; _setting.CsvPdoLayout = value; OnPropertyChanged(); QueueSave(); }
    }

    public int CstPdoNumber
    {
        get => _setting?.CstPdoNumber ?? default;
        set { if (_setting == null || _setting.CstPdoNumber == value) return; _setting.CstPdoNumber = value; OnPropertyChanged(); QueueSave(); }
    }

    public string CstPdoLayout
    {
        get => _setting?.CstPdoLayout ?? "";
        set { if (_setting == null || _setting.CstPdoLayout == value) return; _setting.CstPdoLayout = value; OnPropertyChanged(); QueueSave(); }
    }

    public int FeedbackPdoNumber
    {
        get => _setting?.FeedbackPdoNumber ?? default;
        set { if (_setting == null || _setting.FeedbackPdoNumber == value) return; _setting.FeedbackPdoNumber = value; OnPropertyChanged(); QueueSave(); }
    }

    public string FeedbackPdoLayout
    {
        get => _setting?.FeedbackPdoLayout ?? "";
        set { if (_setting == null || _setting.FeedbackPdoLayout == value) return; _setting.FeedbackPdoLayout = value; OnPropertyChanged(); QueueSave(); }
    }

    public int DiagnosticIndex
    {
        get => _setting?.DiagnosticIndex ?? default;
        set { if (_setting == null || _setting.DiagnosticIndex == value) return; _setting.DiagnosticIndex = value; OnPropertyChanged(); QueueSave(); }
    }

    public int DiagnosticSubIndex
    {
        get => _setting?.DiagnosticSubIndex ?? default;
        set { if (_setting == null || _setting.DiagnosticSubIndex == value) return; _setting.DiagnosticSubIndex = value; OnPropertyChanged(); QueueSave(); }
    }

    public int DiagnosticBytes
    {
        get => _setting?.DiagnosticBytes ?? default;
        set { if (_setting == null || _setting.DiagnosticBytes == value) return; _setting.DiagnosticBytes = value; OnPropertyChanged(); QueueSave(); }
    }

    public long DiagnosticMask
    {
        get => _setting?.DiagnosticMask ?? default;
        set { if (_setting == null || _setting.DiagnosticMask == value) return; _setting.DiagnosticMask = value; OnPropertyChanged(); QueueSave(); }
    }

    public long DiagnosticActiveValue
    {
        get => _setting?.DiagnosticActiveValue ?? default;
        set { if (_setting == null || _setting.DiagnosticActiveValue == value) return; _setting.DiagnosticActiveValue = value; OnPropertyChanged(); QueueSave(); }
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

    public bool IsCANopen => Transport == TransportKind.CANopen;
    public bool IsEtherCAT => Transport == TransportKind.EtherCAT;
	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
