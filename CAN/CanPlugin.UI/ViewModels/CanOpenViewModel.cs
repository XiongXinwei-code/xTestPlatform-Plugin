using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using CAN.Adapters.Zlg;
using System.Text.Json;
using System.Text.RegularExpressions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanOpenViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private readonly PluginCommon.EditorSaveScheduler _saver = new();
    public Action<string, Action>? ExecuteCommand { get => _saver.ExecuteCommand; set => _saver.ExecuteCommand = value; }
    public void FlushPendingSave() => _saver.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanOpenSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _saver.Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _saver.Flush(); _step = step; Load(); }

    private void Load()
    {
        _saver.Flush();
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanOpenSetting)_serializer.CreateDefault();
            SynchronizeZlgSelection();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saver.Schedule(_step, _serializer, _setting);
    }

    public int AdapterType { get => (int)(_setting?.AdapterType ?? CanAdapterType.NI); set { if (_setting == null) return; _setting.AdapterType = (CanAdapterType)value; OnPropertyChanged(); NotifyZlg(); QueueSave(); } }
    public string Channel { get => _setting?.Channel ?? ""; set { if (_setting == null || _setting.Channel == value) return; _setting.Channel = value; SynchronizeZlgSelection(); OnPropertyChanged(); NotifyZlg(); QueueSave(); } }
    public int BaudRate { get => _setting?.BaudRate ?? 500000; set { if (_setting == null || _setting.BaudRate == value) return; _setting.BaudRate = value; OnPropertyChanged(); QueueSave(); } }
    public int Protocol { get => (int)(_setting?.Protocol ?? CanProtocolType.Classic); set { if (_setting == null) return; _setting.Protocol = (CanProtocolType)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDataBitRate)); OnPropertyChanged(nameof(ShowDataSamplePoint)); QueueSave(); } }
    public int DataBitRate { get => _setting?.DataBitRate ?? 2000000; set { if (_setting == null || _setting.DataBitRate == value) return; _setting.DataBitRate = value; OnPropertyChanged(); QueueSave(); } }
    public double DataSamplePoint { get => _setting?.DataSamplePoint ?? 80d; set { if (_setting == null || Math.Abs(_setting.DataSamplePoint - value) < 0.0001) return; _setting.DataSamplePoint = value; OnPropertyChanged(); QueueSave(); } }
    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public int RxQueueSize { get => _setting?.RxQueueSize ?? 8192; set { if (_setting == null || _setting.RxQueueSize == value) return; _setting.RxQueueSize = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string Error => this[nameof(TimeoutMs)];
    public string this[string name] => name switch
    {
        nameof(TimeoutMs) when TimeoutMs <= 0 => "打开总超时必须大于 0 ms",
        nameof(ZlgDeviceIndexToApply) when ZlgDeviceIndexToApply < 0 => "设备索引不能小于 0",
        nameof(ZlgChannelIndexToApply) when ZlgChannelIndexToApply < 0 => "通道索引不能小于 0",
        nameof(ZlgRemotePort) when ZlgRemotePort is < 1 or > 65535 => "端口必须在 1~65535 之间",
        nameof(ZlgLocalPort) when ZlgLocalPort is < 1 or > 65535 => "端口必须在 1~65535 之间",
        nameof(ZlgCloudHttpPort) when ZlgCloudHttpPort is < 1 or > 65535 => "端口必须在 1~65535 之间",
        nameof(ZlgCloudAuthPort) when ZlgCloudAuthPort is < 1 or > 65535 => "端口必须在 1~65535 之间",
        _ => ""
    };

    public bool EnableTermination { get => _setting?.EnableTermination ?? false; set { if (_setting == null || _setting.EnableTermination == value) return; _setting.EnableTermination = value; OnPropertyChanged(); QueueSave(); } }
    public double ArbitrationSamplePoint { get => _setting?.ArbitrationSamplePoint ?? 80d; set { if (_setting == null || Math.Abs(_setting.ArbitrationSamplePoint - value) < 0.0001) return; _setting.ArbitrationSamplePoint = value; OnPropertyChanged(); QueueSave(); } }

    public bool ShowDataBitRate => (_setting?.Protocol ?? CanProtocolType.Classic) != CanProtocolType.Classic;
    public bool ShowDataSamplePoint => ShowDataBitRate;

    public bool IsZlg => (_setting?.AdapterType ?? CanAdapterType.NI) == CanAdapterType.ZLG;
    public IReadOnlyList<ZlgDeviceInfo> ZlgDevices => ZlgDeviceCatalog.Devices;
    private IReadOnlyList<ZlgDeviceInfo> _filteredZlgDevices = ZlgDeviceCatalog.Devices;
    public IReadOnlyList<ZlgDeviceInfo> FilteredZlgDevices => _filteredZlgDevices;
    private string _zlgHardwareFilter = "", _zlgHardwareFilterError = "";
    private IReadOnlyList<ZlgDeviceInfo> _matchingZlgDevices = ZlgDeviceCatalog.Devices;
    private bool _updatingZlgPresentation;
    public string ZlgHardwareFilter
    {
        get => _zlgHardwareFilter;
        set
        {
            if (_zlgHardwareFilter == value) return;
            _zlgHardwareFilter = value ?? ""; OnPropertyChanged();
            try
            {
                if (_zlgHardwareFilter.Length > 256) throw new ArgumentException("筛选表达式最多允许 256 个字符。");
                var pattern = string.IsNullOrWhiteSpace(_zlgHardwareFilter) ? null
                    : new Regex(_zlgHardwareFilter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
                var results = MatchZlgDevices(pattern);
                _matchingZlgDevices = results; _zlgHardwareFilterError = "";
                SetFilteredZlgDevices();
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                _zlgHardwareFilterError = $"型号筛选无效，已保留上次结果及原配置：{ex.Message}";
            }
            OnPropertyChanged(nameof(ZlgHardwareFilterError));
        }
    }
    public string ZlgHardwareFilterError => _zlgHardwareFilterError;

    private IReadOnlyList<ZlgDeviceInfo> MatchZlgDevices(Regex? pattern) => pattern is null ? ZlgDevices
        : ZlgDevices.Where(item => pattern.IsMatch(item.Display)).ToArray();

    private void SetFilteredZlgDevices()
    {
        _updatingZlgPresentation = true;
        try
        {
            _filteredZlgDevices = ZlgDevices.Where(item => item == _zlgDeviceToApply || _matchingZlgDevices.Contains(item)).ToArray();
            OnPropertyChanged(nameof(FilteredZlgDevices));
            OnPropertyChanged(nameof(ZlgDeviceToApply));
        }
        finally { _updatingZlgPresentation = false; }
    }
    private ZlgDeviceInfo? _zlgDeviceToApply;
    public ZlgDeviceInfo? ZlgDeviceToApply
    {
        get => _zlgDeviceToApply;
        set
        {
            if (_updatingZlgPresentation || value is null || _zlgDeviceToApply == value) return;
            _zlgDeviceToApply = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(CanApplyZlgDevice));
            ApplyZlgDevice();
        }
    }
    private int _zlgDeviceIndexToApply, _zlgChannelIndexToApply;
    public int ZlgDeviceIndexToApply { get => _zlgDeviceIndexToApply; set { if (_zlgDeviceIndexToApply == value) return; _zlgDeviceIndexToApply = value; OnPropertyChanged(); ApplyZlgDevice(); } }
    public int ZlgChannelIndexToApply { get => _zlgChannelIndexToApply; set { if (_zlgChannelIndexToApply == value) return; _zlgChannelIndexToApply = value; OnPropertyChanged(); ApplyZlgDevice(); } }
    public bool CanApplyZlgDevice => ZlgDeviceToApply is not null;
    public void ApplyZlgDevice()
    {
        if (_suppressSave || ZlgDeviceToApply is null) return;
        // Preserve invalid numeric input too, so validation cannot silently leave an older valid channel active.
        Channel = JsonSerializer.Serialize($"{ZlgDeviceToApply.Name}/{ZlgDeviceIndexToApply}/{ZlgChannelIndexToApply}");
        FlushPendingSave();
    }

    private void SynchronizeZlgSelection()
    {
        ZlgDeviceInfo? device = null;
        int deviceIndex = 0, channelIndex = 0;
        try
        {
            string? literal = JsonSerializer.Deserialize<string>(Channel);
            string[]? parts = literal?.Trim().Split('/');
            if (parts is { Length: 3 } && int.TryParse(parts[1], out int parsedDevice) && int.TryParse(parts[2], out int parsedChannel))
            {
                uint type = ZlgDeviceCatalog.Parse(parts[0]);
                device = ZlgDevices.FirstOrDefault(item => item.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                    ?? ZlgDevices.First(item => item.Type == type);
                deviceIndex = parsedDevice; channelIndex = parsedChannel;
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { }

        // Updating presentation from Channel must never write a replacement expression back to the setting.
        _zlgDeviceToApply = device;
        _zlgDeviceIndexToApply = deviceIndex;
        _zlgChannelIndexToApply = channelIndex;
        SetFilteredZlgDevices();
        OnPropertyChanged(nameof(CanApplyZlgDevice));
        OnPropertyChanged(nameof(ZlgDeviceIndexToApply)); OnPropertyChanged(nameof(ZlgChannelIndexToApply));
    }
    private uint? ZlgType
    {
        get
        {
            try { string? text = JsonSerializer.Deserialize<string>(Channel); return text is null ? null : ZlgDeviceCatalog.Parse(text.Split('/')[0]); }
            catch (Exception ex) when (ex is JsonException or ArgumentException) { return null; }
        }
    }
    public bool ShowZlgNetwork => IsZlg && (ZlgType is null || ZlgDeviceCatalog.IsNetwork(ZlgType.Value));
    public bool ShowZlgFile => IsZlg && (ZlgType is null || ZlgDeviceCatalog.RequiresFilePath(ZlgType.Value));
    public bool ShowZlgCloud => IsZlg && (ZlgType is null || ZlgType == 46);
    public string ZlgConfigurationHint => ZlgType is { } type ? ZlgDeviceCatalog.Describe(type)
        : "Channel 是动态表达式或未识别型号。显示可选配置入口；运行时仅应用实际型号所需的参数。选择硬件型号会立即替换 Channel，设备和通道索引修改自动生效。";
    private void NotifyZlg()
    {
        OnPropertyChanged(nameof(IsZlg)); OnPropertyChanged(nameof(ShowZlgNetwork)); OnPropertyChanged(nameof(ShowZlgFile));
        OnPropertyChanged(nameof(ShowZlgCloud)); OnPropertyChanged(nameof(ZlgConfigurationHint));
    }
    public string ZlgRemoteAddress { get => _setting?.ZlgRemoteAddress ?? "\"\""; set { if (_setting == null || _setting.ZlgRemoteAddress == value) return; _setting.ZlgRemoteAddress = value; OnPropertyChanged(); QueueSave(); } }
    public string ZlgConfigurationPath { get => _setting?.ZlgConfigurationPath ?? "\"\""; set { if (_setting == null || _setting.ZlgConfigurationPath == value) return; _setting.ZlgConfigurationPath = value; OnPropertyChanged(); QueueSave(); } }
    public string ZlgCloudUserName { get => _setting?.ZlgCloudUserName ?? "\"\""; set { if (_setting == null || _setting.ZlgCloudUserName == value) return; _setting.ZlgCloudUserName = value; OnPropertyChanged(); QueueSave(); } }
    public string ZlgCloudPassword { get => _setting?.ZlgCloudPassword ?? "\"\""; set { if (_setting == null || _setting.ZlgCloudPassword == value) return; _setting.ZlgCloudPassword = value; OnPropertyChanged(); QueueSave(); } }
    public string ZlgCloudHttpServer { get => _setting?.ZlgCloudHttpServer ?? "\"\""; set { if (_setting == null || _setting.ZlgCloudHttpServer == value) return; _setting.ZlgCloudHttpServer = value; OnPropertyChanged(); QueueSave(); } }
    public string ZlgCloudAuthServer { get => _setting?.ZlgCloudAuthServer ?? "\"\""; set { if (_setting == null || _setting.ZlgCloudAuthServer == value) return; _setting.ZlgCloudAuthServer = value; OnPropertyChanged(); QueueSave(); } }
    public int ZlgRemotePort { get => _setting?.ZlgRemotePort ?? 4001; set { if (_setting == null || _setting.ZlgRemotePort == value) return; _setting.ZlgRemotePort = value; OnPropertyChanged(); QueueSave(); } }
    public int ZlgLocalPort { get => _setting?.ZlgLocalPort ?? 4001; set { if (_setting == null || _setting.ZlgLocalPort == value) return; _setting.ZlgLocalPort = value; OnPropertyChanged(); QueueSave(); } }
    public int ZlgNetworkWorkMode { get => _setting?.ZlgNetworkWorkMode ?? 0; set { if (_setting == null || _setting.ZlgNetworkWorkMode == value) return; _setting.ZlgNetworkWorkMode = value; OnPropertyChanged(); QueueSave(); } }
    public int ZlgCloudHttpPort { get => _setting?.ZlgCloudHttpPort ?? 80; set { if (_setting == null || _setting.ZlgCloudHttpPort == value) return; _setting.ZlgCloudHttpPort = value; OnPropertyChanged(); QueueSave(); } }
    public int ZlgCloudAuthPort { get => _setting?.ZlgCloudAuthPort ?? 80; set { if (_setting == null || _setting.ZlgCloudAuthPort == value) return; _setting.ZlgCloudAuthPort = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
