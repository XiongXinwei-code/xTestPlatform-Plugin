param()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root 'obj\ZlgPciSupportTests'
[IO.Directory]::CreateDirectory($work) | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$files = @(
	'Adapters\Zlg\ZlgApi.cs',
	'Adapters\Zlg\ZlgAdapter.cs',
	'Adapters\ICanAdapter.cs',
	'Adapters\CanReceiveDiagnostics.cs',
	'Helpers\CanSamplePointCalculator.cs',
	'Models\CanMessage.cs',
	'Models\CanEnums.cs'
)
$items = ($files | ForEach-Object {
	$path = [Security.SecurityElement]::Escape((Join-Path $root "CAN\CanPlugin\$_"))
	'<Compile Include="' + $path + '" />'
}) -join [Environment]::NewLine
$project = '<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
	<OutputType>Exe</OutputType>
	<TargetFramework>net8.0-windows</TargetFramework>
	<ImplicitUsings>enable</ImplicitUsings>
	<Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>' + $items + '</ItemGroup>
</Project>'
$program = 'using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CAN.Adapters;
using CAN.Adapters.Zlg;
using CAN.Models;

int checks = 0;
void Check(bool condition, string message)
{
	if (!condition) throw new Exception(message);
	checks++;
}
void Reject<T>(Action action, string message) where T : Exception
{
	try { action(); }
	catch (T) { checks++; return; }
	throw new Exception(message);
}
string native = Path.Combine(args[0], "CAN", "CanPlugin", "Native", "Zlg", "x64");
string header = File.ReadAllText(Path.Combine(native, "zlgcan.h"));
var devices = Regex.Matches(header, @"(?m)^#define\s+(ZCAN_(?:PCI|MINI_PCIE)\w*)\s+(\d+)");
Check(devices.Count == 24, "Expected all 24 SDK PCI/PCIe names (23 distinct device codes)");
foreach (Match device in devices)
{
	string name = device.Groups[1].Value;
	uint code = uint.Parse(device.Groups[2].Value);
	Check(ZlgApi.ParseDeviceType(name) == code, name);
	Check(ZlgApi.ParseDeviceType(" " + name.ToLowerInvariant() + " ") == code, name + " lowercase");
	Check(ZlgApi.ParseDeviceType(name[5..].Replace("_", "-")) == code, name + " hyphens");
	Check(ZlgApi.IsCanFdDevice(code) == name.Contains("CANFD"), name + " FD classification");
	Check(ZlgApi.UsesCanFdOnlyController(code) == (code is 60 or 61 or 62 or 63 or 82 or 83 or 84), name + " controller");
	Check(ZlgAdapter.IsPcieCanFdChannel(name + "/0/0") == name.Contains("CANFD"), name + " editor channel classification");
}
foreach (var item in new (string Name, uint Code)[] {
	("USBCAN1", 3), ("USBCAN-I", 3), ("USBCAN2", 4), ("USBCAN-II", 4),
	("USBCAN-E-U", 20), ("USBCAN-2E-U", 21), ("USBCANFD-200U", 41),
	("USBCANFD-100U", 42), ("USBCANFD-MINI", 43), ("PCI-5010-U", 19),
	("PCI-5020-U", 22), ("PCIe9120", 27), ("PCIE-CANFD-200U-M2", 63) })
	Check(ZlgApi.ParseDeviceType(item.Name) == item.Code, item.Name);
foreach (uint code in new uint[] { 41, 42, 43 })
{
	Check(ZlgApi.IsCanFdDevice(code), "USB FD classification");
	Check(!ZlgApi.IsPcieCanFdDevice(code), "USB must not use PCIe path");
	Check(ZlgApi.GetStandardDataSamplePoint(code, 5_000_000) == 80, "USB sample point unchanged");
}
Check(!ZlgAdapter.IsPcieCanFdChannel("USBCANFD-200U/0/0"), "USB FD editor classification");
Check(!ZlgAdapter.IsPcieCanFdChannel("PCI5010U/0/0"), "PCI Classic editor classification");
Reject<ArgumentException>(() => ZlgAdapter.IsPcieCanFdChannel("UNKNOWN/0/0"), "Unknown editor model accepted");
Check(ZlgApi.UsesDriverClassicBitRate(19) && ZlgApi.UsesDriverClassicBitRate(22), "PCI50xxU standard bit rate");
Check(!ZlgApi.UsesDriverClassicBitRate(27), "Legacy PCIe BTR path");
Reject<ArgumentException>(() => ZlgApi.ParseDeviceType("UNKNOWN"), "Unknown device accepted");
Reject<ArgumentException>(() => ZlgApi.ParseDeviceType("38"), "Numeric bypass accepted");
Check(Marshal.SizeOf<ZlgApi.ZCAN_CHANNEL_INIT_CONFIG>() == 32, "Native union size changed");
Check(Marshal.OffsetOf<ZlgApi.ZCAN_CHANNEL_INIT_CONFIG>("can").ToInt32() == 4, "CAN union offset");
Check(Marshal.OffsetOf<ZlgApi.ZCAN_CHANNEL_INIT_CONFIG>("canfd").ToInt32() == 4, "FD union offset");
string ini = File.ReadAllText(Path.Combine(native, "kerneldlls", "dll_cfg.ini"));
foreach (Match mapping in Regex.Matches(ini, @"(?m)^\d+=([^\s]+)\s+(\d+)\s"))
{
	uint code = uint.Parse(mapping.Groups[2].Value);
	if (!devices.Cast<Match>().Any(d => uint.Parse(d.Groups[2].Value) == code)) continue;
	Check(ZlgApi.GetDeviceOpenDiagnostics(code).Contains(mapping.Groups[1].Value), "Kernel mapping " + code);
}
Check(ZlgApi.GetDeviceOpenDiagnostics(18).Contains("dll_cfg.ini"), "PCIe9220 unsupported SDK diagnostic");
foreach (string file in Directory.GetFiles(Path.Combine(native, "kerneldlls", "devices_property"), "*pcie*.xml"))
{
	string model = Path.GetFileNameWithoutExtension(file);
	uint code = ZlgApi.ParseDeviceType(model);
	var channel = XDocument.Load(file).Descendants("channel_0").Single();
	int[] arbitration = channel.Element("canfd_abit_baud_rate")!.Descendants("option")
		.Select(o => int.Parse(o.Attribute("value")!.Value)).Where(v => v != 0).ToArray();
	foreach (int rate in arbitration) ZlgApi.ValidatePcieBitRates(code, rate, null);
	foreach (var option in channel.Element("canfd_dbit_baud_rate")!.Descendants("option"))
	{
		int rate = int.Parse(option.Attribute("value")!.Value);
		ZlgApi.ValidatePcieBitRates(code, arbitration[0], rate);
		double samplePoint = double.Parse(Regex.Match(option.Attribute("desc")!.Value, @"(\d+)%").Groups[1].Value, CultureInfo.InvariantCulture);
		Check(ZlgApi.GetStandardDataSamplePoint(code, rate) == samplePoint, model + " data sample point");
	}
	foreach (int rate in new[] { 50_000, 100_000, 125_000, 250_000, 500_000, 800_000, 1_000_000 })
	{
		if (arbitration.Contains(rate)) continue;
		Reject<ArgumentException>(() => ZlgApi.ValidatePcieBitRates(code, rate, null), model + " unsupported arbitration rate");
	}
	Reject<ArgumentException>(() => ZlgApi.ValidatePcieBitRates(code, 123_456, null), model + " custom arbitration rate");
	Reject<ArgumentException>(() => ZlgApi.ValidatePcieBitRates(code, arbitration[0], 123_456), model + " custom data rate");
	bool fdOnly = channel.Element("protocol")!.Descendants("option").All(o => o.Attribute("value")!.Value == "1");
	Check(ZlgApi.UsesCanFdOnlyController(code) == fdOnly, model + " protocol options");
	Check(channel.Element("canfd_exp") != null && channel.Element("canfd_standard") == null, model + " PCIe properties");
}
void RejectOpen<T>(CanAdapterConfig config) where T : Exception
{
	using var adapter = new ZlgAdapter();
	Reject<T>(() => adapter.Open(config), "Invalid configuration reached native OpenDevice");
	Check(!adapter.IsConnected, "Failed open left adapter connected");
}
RejectOpen<ArgumentException>(new() { Channel = "PCIe9120/0" });
RejectOpen<ArgumentException>(new() { Channel = "PCIe9120/-1/0" });
RejectOpen<InvalidOperationException>(new() { Channel = "PCIe9120/0/0", Protocol = CanProtocolType.FD });
RejectOpen<InvalidOperationException>(new() { Channel = "PCIE-CANFD-200U/0/0", ArbitrationSamplePoint = 75 });
RejectOpen<InvalidOperationException>(new() { Channel = "PCIE-CANFD-200U/0/0", ArbitrationSamplePoint = double.NaN });
RejectOpen<InvalidOperationException>(new() { Channel = "PCIE-CANFD-200U/0/0", Protocol = CanProtocolType.FD, DataBitRate = 5_000_000, DataSamplePoint = 80 });
RejectOpen<InvalidOperationException>(new() { Channel = "USBCANFD-200U/0/0", Protocol = CanProtocolType.FD, DataSamplePoint = 75 });
RejectOpen<ArgumentException>(new() { Channel = "PCIE-CANFD-200U/0/0", BaudRate = 50_000 });
RejectOpen<ArgumentException>(new() { Channel = "PCI5010U/0/0", BaudRate = 25_000 });
Console.WriteLine($"Passed {checks} checks; all SDK PCI/PCIe names covered. No hardware opened.");
'
[IO.File]::WriteAllText((Join-Path $work 'ZlgPciSupportTests.csproj'), $project, $utf8)
[IO.File]::WriteAllText((Join-Path $work 'Program.cs'), $program, $utf8)
& dotnet run --project (Join-Path $work 'ZlgPciSupportTests.csproj') --configuration Release -- $root
if ($LASTEXITCODE -ne 0) { throw 'ZLG PCI/PCIe regression checks failed.' }
