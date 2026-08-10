# Create Modbus Plugin UI layer
$modbusRoot = $PSScriptRoot

# ============================================================
# ModbusPlugin.UI.csproj
# ============================================================
@'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
	<TargetFramework>net8.0-windows</TargetFramework>
	<Nullable>enable</Nullable>
	<UseWPF>true</UseWPF>
	<ImplicitUsings>enable</ImplicitUsings>
	<AssemblyName>Modbus.StepPlugin.UI</AssemblyName>
	<RootNamespace>Modbus.UI</RootNamespace>
	<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
	<OutputPath>..\..\..\xTestPlatform\xTestPlatform\bin\$(Configuration)\$(TargetFramework)\win-x64\Plugins\Modbus\</OutputPath>
	<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
	<Resource Include="Resources\Icons\modbus.png">
	  <CopyToOutputDirectory>Never</CopyToOutputDirectory>
	</Resource>
  </ItemGroup>

  <ItemGroup>
	<ProjectReference Include="..\ModbusPlugin\ModbusPlugin.csproj" />
	<PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.1.2" />
	<PackageReference Include="MessagePack" Version="3.1.7" />
	<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
	<PackageReference Include="Syncfusion.Themes.Windows11Light.WPF" Version="32.1.25" />
	<PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="32.1.25" />
	<PackageReference Include="Syncfusion.Tools.WPF" Version="32.1.25" />
  </ItemGroup>

</Project>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ModbusPlugin.UI.csproj" -Encoding UTF8

# ============================================================
# ViewModels/ModbusConnectViewModel.cs
# ============================================================
@'
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
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusConnectViewModel.cs" -Encoding UTF8

# ============================================================
# ViewModels/ModbusDisconnectViewModel.cs
# ============================================================
@'
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
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusDisconnectViewModel.cs" -Encoding UTF8

# ============================================================
# ViewModels/ModbusReadViewModel.cs
# ============================================================
@'
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
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusReadViewModel.cs" -Encoding UTF8

# ============================================================
# ViewModels/ModbusWriteViewModel.cs
# ============================================================
@'
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
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusWriteViewModel.cs" -Encoding UTF8

# ============================================================
# ViewModels/ModbusBatchReadViewModel.cs
# ============================================================
@'
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
	public void NotifyItemChanged() => QueueSave();

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusBatchReadViewModel.cs" -Encoding UTF8

# ============================================================
# ViewModels/ModbusBatchWriteViewModel.cs
# ============================================================
@'
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
	public void NotifyItemChanged() => QueueSave();

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ViewModels\ModbusBatchWriteViewModel.cs" -Encoding UTF8

Write-Host "Modbus UI ViewModels created successfully!" -ForegroundColor Green
