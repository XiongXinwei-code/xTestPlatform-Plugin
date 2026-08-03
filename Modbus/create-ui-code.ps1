# Create UI code files using WriteAllText
$modbusRoot = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$vmDir = "$modbusRoot\ModbusPlugin.UI\ViewModels"
$viewDir = "$modbusRoot\ModbusPlugin.UI\Views"

# Ensure dirs
New-Item -Path $vmDir -ItemType Directory -Force | Out-Null
New-Item -Path $viewDir -ItemType Directory -Force | Out-Null

# ===== ViewModels =====
$content = @"
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusConnectViewModel : INotifyPropertyChanged
{
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
"@
[IO.File]::WriteAllText("$vmDir\ModbusConnectViewModel.cs", $content, $utf8)

$content = @"
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusDisconnectViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusDisconnectSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusDisconnectSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusDisconnectSetting)_serializer.CreateDefault();
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

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
"@
[IO.File]::WriteAllText("$vmDir\ModbusDisconnectViewModel.cs", $content, $utf8)

$content = @"
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusReadViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusReadSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

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

	private void QueueSave()
	{
		if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
		_saveCts?.Cancel();
		var cts = _saveCts = new CancellationTokenSource();
		_ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
	}

	public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
	public int SlaveAddress { get => _setting?.SlaveAddress ?? 1; set { if (_setting == null || _setting.SlaveAddress == (byte)value) return; _setting.SlaveAddress = (byte)value; OnPropertyChanged(); QueueSave(); } }
	public int RegisterType { get => (int)(_setting?.RegisterType ?? ModbusRegisterType.HoldingRegister); set { if (_setting == null) return; _setting.RegisterType = (ModbusRegisterType)value; OnPropertyChanged(); QueueSave(); } }
	public string StartAddress { get => _setting?.StartAddress ?? "0"; set { if (_setting == null || _setting.StartAddress == value) return; _setting.StartAddress = value; OnPropertyChanged(); QueueSave(); } }
	public string Quantity { get => _setting?.Quantity ?? "1"; set { if (_setting == null || _setting.Quantity == value) return; _setting.Quantity = value; OnPropertyChanged(); QueueSave(); } }
	public int DataFormat { get => (int)(_setting?.DataFormat ?? ModbusDataFormat.UInt16); set { if (_setting == null) return; _setting.DataFormat = (ModbusDataFormat)value; OnPropertyChanged(); QueueSave(); } }
	public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
"@
[IO.File]::WriteAllText("$vmDir\ModbusReadViewModel.cs", $content, $utf8)

$content = @"
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusWriteViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusWriteSetting? _setting;

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusWriteSetting)_serializer.CreateDefault();
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
	public int SlaveAddress { get => _setting?.SlaveAddress ?? 1; set { if (_setting == null || _setting.SlaveAddress == (byte)value) return; _setting.SlaveAddress = (byte)value; OnPropertyChanged(); QueueSave(); } }
	public int RegisterType { get => (int)(_setting?.RegisterType ?? ModbusRegisterType.HoldingRegister); set { if (_setting == null) return; _setting.RegisterType = (ModbusRegisterType)value; OnPropertyChanged(); QueueSave(); } }
	public string StartAddress { get => _setting?.StartAddress ?? "0"; set { if (_setting == null || _setting.StartAddress == value) return; _setting.StartAddress = value; OnPropertyChanged(); QueueSave(); } }
	public string Values { get => _setting?.Values ?? "0"; set { if (_setting == null || _setting.Values == value) return; _setting.Values = value; OnPropertyChanged(); QueueSave(); } }
	public int DataFormat { get => (int)(_setting?.DataFormat ?? ModbusDataFormat.UInt16); set { if (_setting == null) return; _setting.DataFormat = (ModbusDataFormat)value; OnPropertyChanged(); QueueSave(); } }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
"@
[IO.File]::WriteAllText("$vmDir\ModbusWriteViewModel.cs", $content, $utf8)

$content = @"
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusBatchReadViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusBatchReadSetting? _setting;

	public ObservableCollection<ModbusBatchItem> Items { get; } = new();

	public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
	public void AttachStep(Step step) { _step = step; Load(); }

	private void Load()
	{
		if (_serializer == null || _step == null) return;
		_suppressSave = true;
		try
		{
			_setting = _step.StepSetting.Setting is { Length: > 0 } d
				? (ModbusBatchReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
				: (ModbusBatchReadSetting)_serializer.CreateDefault();
			Items.Clear();
			foreach (var item in _setting.Items) Items.Add(item);
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	private void QueueSave()
	{
		if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
		_setting.Items = Items.ToList();
		_saveCts?.Cancel();
		var cts = _saveCts = new CancellationTokenSource();
		_ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
	}

	public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
	public int IntervalMs { get => _setting?.IntervalMs ?? 0; set { if (_setting == null || _setting.IntervalMs == value) return; _setting.IntervalMs = value; OnPropertyChanged(); QueueSave(); } }

	public void AddItem() { Items.Add(new ModbusBatchItem()); QueueSave(); }
	public void RemoveItem(ModbusBatchItem item) { Items.Remove(item); QueueSave(); }

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
"@
[IO.File]::WriteAllText("$vmDir\ModbusBatchReadViewModel.cs", $content, $utf8)

$content = @"
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.ViewModels;

public class ModbusBatchWriteViewModel : INotifyPropertyChanged
{
	private const int SaveDebounceMs = 200;
	private CancellationTokenSource? _saveCts;
	private bool _suppressSave;
	private Step? _step;
	private IStepSettingSerializer? _serializer;
	private ModbusBatchWriteSetting? _setting;

	public ObservableCollection<ModbusBatchWriteItem> Items { get; } = new();

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
			Items.Clear();
			foreach (var item in _setting.Items) Items.Add(item);
			OnPropertyChanged(string.Empty);
		}
		finally { _suppressSave = false; }
	}

	private void QueueSave()
	{
		if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
		_setting.Items = Items.ToList();
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
"@
[IO.File]::WriteAllText("$vmDir\ModbusBatchWriteViewModel.cs", $content, $utf8)

# ===== Views code-behind =====
$content = @"
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusConnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusConnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusConnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusConnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusConnectEditorView.xaml.cs", $content, $utf8)

$content = @"
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusDisconnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusDisconnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusDisconnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusDisconnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusDisconnectEditorView.xaml.cs", $content, $utf8)

$content = @"
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusReadEditorView.xaml.cs", $content, $utf8)

$content = @"
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusWriteEditorView.xaml.cs", $content, $utf8)

$content = @"
using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusBatchReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusBatchReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchItem item) ViewModel.RemoveItem(item);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusBatchReadEditorView.xaml.cs", $content, $utf8)

$content = @"
using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusBatchWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusBatchWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchWriteItem item) ViewModel.RemoveItem(item);
	}
}
"@
[IO.File]::WriteAllText("$viewDir\ModbusBatchWriteEditorView.xaml.cs", $content, $utf8)

# ===== EditorPlugins =====
$content = @"
using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.UI;

public sealed class ModbusConnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusConnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusConnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusConnectSetting)new ModbusConnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_001", "ConnectionName is required"));
		if (s.TransportType == ModbusTransportType.TCP && string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("MB_002", "IP Address is required for TCP"));
		if (s.TransportType == ModbusTransportType.RTU && string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("MB_003", "PortName is required for RTU"));
		return errors;
	}
}

public sealed class ModbusDisconnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusDisconnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusDisconnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusDisconnectSetting)new ModbusDisconnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_010", "ConnectionName is required"));
		return errors;
	}
}

public sealed class ModbusReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusReadSetting)new ModbusReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_020", "ConnectionName is required"));
		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_021", "ResultVariable is required"));
		return errors;
	}
}

public sealed class ModbusWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusWriteSetting)new ModbusWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_030", "ConnectionName is required"));
		if (string.IsNullOrWhiteSpace(s.Values))
			errors.Add(StepSettingError.Error("MB_031", "Values is required"));
		return errors;
	}
}

public sealed class ModbusBatchReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchReadSetting)new ModbusBatchReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_040", "ConnectionName is required"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_041", "Batch read list is empty"));
		return errors;
	}
}

public sealed class ModbusBatchWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchWriteSetting)new ModbusBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_050", "ConnectionName is required"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_051", "Batch write list is empty"));
		return errors;
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin.UI\ModbusEditorPlugins.cs", $content, $utf8)

Write-Host "UI code files created successfully!" -ForegroundColor Green
