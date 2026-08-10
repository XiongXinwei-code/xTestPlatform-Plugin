# Create Modbus Plugin project structure
$root = Split-Path -Parent $PSScriptRoot
$modbusRoot = $PSScriptRoot

# Create directories
$dirs = @(
	"$modbusRoot\ModbusPlugin\Models",
	"$modbusRoot\ModbusPlugin\Helpers",
	"$modbusRoot\ModbusPlugin\Executors",
	"$modbusRoot\ModbusPlugin.UI\ViewModels",
	"$modbusRoot\ModbusPlugin.UI\Views",
	"$modbusRoot\ModbusPlugin.UI\Resources\Icons"
)
foreach ($d in $dirs) {
	New-Item -Path $d -ItemType Directory -Force | Out-Null
}

# ============================================================
# ModbusPlugin.csproj
# ============================================================
@'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
	<TargetFramework>net8.0-windows</TargetFramework>
	<Nullable>enable</Nullable>
	<ImplicitUsings>enable</ImplicitUsings>
	<AssemblyName>Modbus.StepPlugin</AssemblyName>
	<RootNamespace>Modbus</RootNamespace>
	<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
	<OutputPath>..\..\..\xTestPlatform\xTestPlatform\bin\$(Configuration)\$(TargetFramework)\win-x64\Plugins\Modbus\</OutputPath>
	<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
	<PackageReference Include="MessagePack" Version="3.1.7" />
	<PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.1.2" />
	<PackageReference Include="NModbus" Version="3.0.81" />
	<PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>

</Project>
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusPlugin.csproj" -Encoding UTF8

# ============================================================
# Models/ModbusEnums.cs
# ============================================================
@'
namespace Modbus.Models;

public enum ModbusTransportType
{
	TCP = 0,
	RTU = 1
}

public enum ModbusRegisterType
{
	Coil = 0,
	DiscreteInput = 1,
	HoldingRegister = 2,
	InputRegister = 3
}

public enum ModbusDataFormat
{
	UInt16 = 0,
	Int16 = 1,
	UInt32_AB_CD = 2,
	Int32_AB_CD = 3,
	Float_AB_CD = 4,
	UInt32_CD_AB = 5,
	Int32_CD_AB = 6,
	Float_CD_AB = 7
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusEnums.cs" -Encoding UTF8

# ============================================================
# Models/ModbusConnectSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusConnectSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>传输类型 TCP/RTU</summary>
	public ModbusTransportType TransportType { get; set; } = ModbusTransportType.TCP;

	// --- TCP ---
	/// <summary>IP 地址</summary>
	[ExpressionField]
	public string IpAddress { get; set; } = "192.168.1.1";

	/// <summary>TCP 端口</summary>
	public int TcpPort { get; set; } = 502;

	// --- RTU ---
	/// <summary>串口名称</summary>
	[ExpressionField]
	public string PortName { get; set; } = "COM1";

	/// <summary>波特率</summary>
	public int BaudRate { get; set; } = 9600;

	/// <summary>数据位</summary>
	public int DataBits { get; set; } = 8;

	/// <summary>停止位 (1=One, 2=Two)</summary>
	public int StopBits { get; set; } = 1;

	/// <summary>校验 (0=None, 1=Odd, 2=Even)</summary>
	public int Parity { get; set; } = 0;

	/// <summary>超时(ms)</summary>
	public int TimeoutMs { get; set; } = 3000;
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusConnectSetting.cs" -Encoding UTF8

# ============================================================
# Models/ModbusDisconnectSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusDisconnectSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusDisconnectSetting.cs" -Encoding UTF8

# ============================================================
# Models/ModbusReadSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusReadSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器类型</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	/// <summary>读取数量</summary>
	[ExpressionField]
	public string Quantity { get; set; } = "1";

	/// <summary>数据格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>结果保存变量名</summary>
	[ExpressionField]
	public string ResultVariable { get; set; } = "ModbusResult";
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusReadSetting.cs" -Encoding UTF8

# ============================================================
# Models/ModbusWriteSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusWriteSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器类型 (Coil / HoldingRegister)</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	/// <summary>要写入的值（逗号分隔，如 "100,200,300"）</summary>
	[ExpressionField]
	public string Values { get; set; } = "0";

	/// <summary>数据格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusWriteSetting.cs" -Encoding UTF8

# ============================================================
# Models/ModbusBatchItem.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchItem
{
	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器类型</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>数量</summary>
	public ushort Quantity { get; set; } = 1;

	/// <summary>数据格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	/// <summary>结果变量名</summary>
	public string ResultVariable { get; set; } = "";
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusBatchItem.cs" -Encoding UTF8

# ============================================================
# Models/ModbusBatchReadSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchReadSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>批量读取项列表</summary>
	public List<ModbusBatchItem> Items { get; set; } = new();

	/// <summary>读取间隔(ms)，0=无间隔</summary>
	public int IntervalMs { get; set; } = 0;
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusBatchReadSetting.cs" -Encoding UTF8

# ============================================================
# Models/ModbusBatchWriteItem.cs
# ============================================================
@'
using MessagePack;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteItem
{
	/// <summary>从站地址</summary>
	public byte SlaveAddress { get; set; } = 1;

	/// <summary>寄存器类型 (Coil/HoldingRegister)</summary>
	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	/// <summary>起始地址</summary>
	public ushort StartAddress { get; set; } = 0;

	/// <summary>写入值(逗号分隔)</summary>
	public string Values { get; set; } = "0";

	/// <summary>数据格式</summary>
	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusBatchWriteItem.cs" -Encoding UTF8

# ============================================================
# Models/ModbusBatchWriteSetting.cs
# ============================================================
@'
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteSetting
{
	/// <summary>连接标识名</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>批量写入项列表</summary>
	public List<ModbusBatchWriteItem> Items { get; set; } = new();

	/// <summary>写入间隔(ms)，0=无间隔</summary>
	public int IntervalMs { get; set; } = 0;
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Models\ModbusBatchWriteSetting.cs" -Encoding UTF8

# ============================================================
# Helpers/ModbusHelper.cs
# ============================================================
@'
namespace Modbus.Helpers;

public static class ModbusHelper
{
	public static string GetConnectionKey(string connectionName) => $"__Modbus_{connectionName}";
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Helpers\ModbusHelper.cs" -Encoding UTF8

# ============================================================
# Helpers/ModbusDataConverter.cs
# ============================================================
@'
using Modbus.Models;

namespace Modbus.Helpers;

public static class ModbusDataConverter
{
	public static object ConvertRegisters(ushort[] registers, ModbusDataFormat format)
	{
		if (registers.Length == 0) return Array.Empty<ushort>();

		return format switch
		{
			ModbusDataFormat.UInt16 => registers.Length == 1 ? registers[0] : registers,
			ModbusDataFormat.Int16 => registers.Length == 1
				? (object)(short)registers[0]
				: registers.Select(r => (short)r).ToArray(),
			ModbusDataFormat.UInt32_AB_CD => ConvertPairs(registers, (a, b) => (uint)((a << 16) | b)),
			ModbusDataFormat.Int32_AB_CD => ConvertPairs(registers, (a, b) => (int)((a << 16) | b)),
			ModbusDataFormat.Float_AB_CD => ConvertPairs(registers, (a, b) =>
			{
				var bytes = BitConverter.GetBytes((uint)((a << 16) | b));
				return BitConverter.ToSingle(bytes, 0);
			}),
			ModbusDataFormat.UInt32_CD_AB => ConvertPairs(registers, (a, b) => (uint)((b << 16) | a)),
			ModbusDataFormat.Int32_CD_AB => ConvertPairs(registers, (a, b) => (int)((b << 16) | a)),
			ModbusDataFormat.Float_CD_AB => ConvertPairs(registers, (a, b) =>
			{
				var bytes = BitConverter.GetBytes((uint)((b << 16) | a));
				return BitConverter.ToSingle(bytes, 0);
			}),
			_ => registers
		};
	}

	private static object ConvertPairs<T>(ushort[] registers, Func<ushort, ushort, T> converter)
	{
		var results = new List<T>();
		for (int i = 0; i + 1 < registers.Length; i += 2)
			results.Add(converter(registers[i], registers[i + 1]));
		if (results.Count == 1) return results[0]!;
		return results.ToArray();
	}

	public static ushort[] ConvertToRegisters(string valuesStr, ModbusDataFormat format)
	{
		var parts = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		return format switch
		{
			ModbusDataFormat.UInt16 => parts.Select(p => ushort.Parse(p)).ToArray(),
			ModbusDataFormat.Int16 => parts.Select(p => (ushort)(short.Parse(p))).ToArray(),
			ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.Int32_AB_CD =>
				parts.SelectMany(p => { var v = uint.Parse(p); return new ushort[] { (ushort)(v >> 16), (ushort)(v & 0xFFFF) }; }).ToArray(),
			ModbusDataFormat.Float_AB_CD =>
				parts.SelectMany(p => { var v = BitConverter.ToUInt32(BitConverter.GetBytes(float.Parse(p)), 0); return new ushort[] { (ushort)(v >> 16), (ushort)(v & 0xFFFF) }; }).ToArray(),
			ModbusDataFormat.UInt32_CD_AB or ModbusDataFormat.Int32_CD_AB =>
				parts.SelectMany(p => { var v = uint.Parse(p); return new ushort[] { (ushort)(v & 0xFFFF), (ushort)(v >> 16) }; }).ToArray(),
			ModbusDataFormat.Float_CD_AB =>
				parts.SelectMany(p => { var v = BitConverter.ToUInt32(BitConverter.GetBytes(float.Parse(p)), 0); return new ushort[] { (ushort)(v & 0xFFFF), (ushort)(v >> 16) }; }).ToArray(),
			_ => parts.Select(p => ushort.Parse(p)).ToArray()
		};
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Helpers\ModbusDataConverter.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusConnectExecutor.cs
# ============================================================
@'
using System.IO.Ports;
using System.Net.Sockets;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusConnectExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusConnectPlugin().CreateSerializer();
		var setting = (ModbusConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			IModbusMaster master;
			object transport;
			var factory = new ModbusFactory();

			if (setting.TransportType == ModbusTransportType.TCP)
			{
				var ip = await Evaluator.EvaluateAsync<string>(setting.IpAddress, context) ?? setting.IpAddress;
				var client = new TcpClient();
				await client.ConnectAsync(ip, setting.TcpPort, cancellationToken);
				client.ReceiveTimeout = setting.TimeoutMs;
				client.SendTimeout = setting.TimeoutMs;
				master = factory.CreateMaster(client);
				transport = client;
			}
			else
			{
				var portName = await Evaluator.EvaluateAsync<string>(setting.PortName, context) ?? setting.PortName;
				var port = new SerialPort(portName, setting.BaudRate, (Parity)setting.Parity, setting.DataBits, (StopBits)setting.StopBits);
				port.ReadTimeout = setting.TimeoutMs;
				port.WriteTimeout = setting.TimeoutMs;
				port.Open();
				master = factory.CreateRtuMaster(port);
				transport = port;
			}

			master.Transport.ReadTimeout = setting.TimeoutMs;
			master.Transport.WriteTimeout = setting.TimeoutMs;

			context.CurrentStep.RuntimeData[key] = master;
			context.CurrentStep.RuntimeData[key + "_transport"] = transport;

			context.LogAction?.Invoke($"Modbus 连接已建立: {connName} ({setting.TransportType})");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已连接: {connName}" }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 连接失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusConnectExecutor.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusDisconnectExecutor.cs
# ============================================================
@'
using System.IO.Ports;
using System.Net.Sockets;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusDisconnectExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusDisconnectPlugin().CreateSerializer();
		var setting = (ModbusDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is IModbusMaster master)
			{
				master.Dispose();
				context.CurrentStep.RuntimeData.Remove(key);
			}

			if (context.CurrentStep.RuntimeData.TryGetValue(key + "_transport", out var tObj))
			{
				if (tObj is TcpClient tcp) tcp.Dispose();
				else if (tObj is SerialPort sp) { sp.Close(); sp.Dispose(); }
				context.CurrentStep.RuntimeData.Remove(key + "_transport");
			}

			context.LogAction?.Invoke($"Modbus 连接已关闭: {connName}");
			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已断开: {connName}" }
			};
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 断开失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusDisconnectExecutor.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusReadExecutor.cs
# ============================================================
@'
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusReadPlugin().CreateSerializer();
		var setting = (ModbusReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"未找到 Modbus 连接: {connName}" }
					}
				};
			}

			var startAddr = ushort.Parse(await Evaluator.EvaluateAsync<string>(setting.StartAddress, context) ?? setting.StartAddress);
			var quantity = ushort.Parse(await Evaluator.EvaluateAsync<string>(setting.Quantity, context) ?? setting.Quantity);

			object result;
			switch (setting.RegisterType)
			{
				case ModbusRegisterType.Coil:
					var coils = await master.ReadCoilsAsync(setting.SlaveAddress, startAddr, quantity);
					result = coils.Length == 1 ? coils[0] : coils;
					break;
				case ModbusRegisterType.DiscreteInput:
					var inputs = await master.ReadInputsAsync(setting.SlaveAddress, startAddr, quantity);
					result = inputs.Length == 1 ? inputs[0] : inputs;
					break;
				case ModbusRegisterType.HoldingRegister:
					var holdRegs = await master.ReadHoldingRegistersAsync(setting.SlaveAddress, startAddr, quantity);
					result = ModbusDataConverter.ConvertRegisters(holdRegs, setting.DataFormat);
					break;
				case ModbusRegisterType.InputRegister:
					var inRegs = await master.ReadInputRegistersAsync(setting.SlaveAddress, startAddr, quantity);
					result = ModbusDataConverter.ConvertRegisters(inRegs, setting.DataFormat);
					break;
				default:
					result = "Unknown register type";
					break;
			}

			var varName = await Evaluator.EvaluateAsync<string>(setting.ResultVariable, context) ?? setting.ResultVariable;
			context.SetVariable(varName, result);

			context.LogAction?.Invoke($"Modbus Read: Addr={startAddr}, Qty={quantity}, Result={result}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = result?.ToString() }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 读取失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusReadExecutor.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusWriteExecutor.cs
# ============================================================
@'
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusWritePlugin().CreateSerializer();
		var setting = (ModbusWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"未找到 Modbus 连接: {connName}" }
					}
				};
			}

			var startAddr = ushort.Parse(await Evaluator.EvaluateAsync<string>(setting.StartAddress, context) ?? setting.StartAddress);
			var valuesStr = await Evaluator.EvaluateAsync<string>(setting.Values, context) ?? setting.Values;

			if (setting.RegisterType == ModbusRegisterType.Coil)
			{
				var bools = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Select(v => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)).ToArray();
				if (bools.Length == 1)
					await master.WriteSingleCoilAsync(setting.SlaveAddress, startAddr, bools[0]);
				else
					await master.WriteMultipleCoilsAsync(setting.SlaveAddress, startAddr, bools);
			}
			else
			{
				var registers = ModbusDataConverter.ConvertToRegisters(valuesStr, setting.DataFormat);
				if (registers.Length == 1)
					await master.WriteSingleRegisterAsync(setting.SlaveAddress, startAddr, registers[0]);
				else
					await master.WriteMultipleRegistersAsync(setting.SlaveAddress, startAddr, registers);
			}

			context.LogAction?.Invoke($"Modbus Write: Addr={startAddr}, Values={valuesStr}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已写入: Addr={startAddr}" }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 写入失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusWriteExecutor.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusBatchReadExecutor.cs
# ============================================================
@'
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusBatchReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchReadPlugin().CreateSerializer();
		var setting = (ModbusBatchReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"未找到 Modbus 连接: {connName}" }
					}
				};
			}

			var results = new List<string>();
			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				object result;
				switch (item.RegisterType)
				{
					case ModbusRegisterType.Coil:
						var coils = await master.ReadCoilsAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = coils.Length == 1 ? coils[0] : coils;
						break;
					case ModbusRegisterType.DiscreteInput:
						var inputs = await master.ReadInputsAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = inputs.Length == 1 ? inputs[0] : inputs;
						break;
					case ModbusRegisterType.HoldingRegister:
						var holdRegs = await master.ReadHoldingRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = ModbusDataConverter.ConvertRegisters(holdRegs, item.DataFormat);
						break;
					default:
						var inRegs = await master.ReadInputRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = ModbusDataConverter.ConvertRegisters(inRegs, item.DataFormat);
						break;
				}

				if (!string.IsNullOrWhiteSpace(item.ResultVariable))
					context.SetVariable(item.ResultVariable, result);

				results.Add($"{item.ResultVariable}={result}");

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			var summary = string.Join("; ", results);
			context.LogAction?.Invoke($"Modbus BatchRead: {summary}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = summary }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 批量读取失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusBatchReadExecutor.cs" -Encoding UTF8

# ============================================================
# Executors/ModbusBatchWriteExecutor.cs
# ============================================================
@'
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusBatchWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchWritePlugin().CreateSerializer();
		var setting = (ModbusBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"未找到 Modbus 连接: {connName}" }
					}
				};
			}

			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (item.RegisterType == ModbusRegisterType.Coil)
				{
					var bools = item.Values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
						.Select(v => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)).ToArray();
					if (bools.Length == 1)
						await master.WriteSingleCoilAsync(item.SlaveAddress, item.StartAddress, bools[0]);
					else
						await master.WriteMultipleCoilsAsync(item.SlaveAddress, item.StartAddress, bools);
				}
				else
				{
					var registers = ModbusDataConverter.ConvertToRegisters(item.Values, item.DataFormat);
					if (registers.Length == 1)
						await master.WriteSingleRegisterAsync(item.SlaveAddress, item.StartAddress, registers[0]);
					else
						await master.WriteMultipleRegistersAsync(item.SlaveAddress, item.StartAddress, registers);
				}

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			context.LogAction?.Invoke($"Modbus BatchWrite: {setting.Items.Count} items written");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已写入 {setting.Items.Count} 项" }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 批量写入失败: {ex.Message}" }
				}
			};
		}
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\Executors\ModbusBatchWriteExecutor.cs" -Encoding UTF8

# ============================================================
# Plugin classes
# ============================================================
@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusConnectPlugin : StepPluginBase<ModbusConnectSetting>
{
	public override string StepTypeId => "IO.ModbusConnect";
	public override string DisplayName => "Modbus_Connect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"建立 Modbus 连接，支持 TCP 和 RTU(串口) 两种传输方式。" +
		"Setting 字段：ConnectionName(string,表达式,连接标识名), TransportType(枚举,TCP/RTU), " +
		"IpAddress(string,表达式,TCP地址), TcpPort(int,TCP端口), " +
		"PortName(string,表达式,串口名), BaudRate(int), DataBits(int), StopBits(int), Parity(int), TimeoutMs(int)。";

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusConnectPlugin.cs" -Encoding UTF8

@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusDisconnectPlugin : StepPluginBase<ModbusDisconnectSetting>
{
	public override string StepTypeId => "IO.ModbusDisconnect";
	public override string DisplayName => "Modbus_Disconnect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"关闭指定的 Modbus 连接。" +
		"Setting 字段：ConnectionName(string,表达式,连接标识名)。";

	public override IStepExecutor CreateExecutor() => new ModbusDisconnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Disconnect {s.ConnectionName}";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusDisconnectPlugin.cs" -Encoding UTF8

@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusReadPlugin : StepPluginBase<ModbusReadSetting>
{
	public override string StepTypeId => "IO.ModbusRead";
	public override string DisplayName => "Modbus_Read";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"从 Modbus 设备读取数据，支持线圈、离散输入、保持寄存器、输入寄存器。" +
		"Setting 字段：ConnectionName(string,表达式), SlaveAddress(byte), RegisterType(枚举), " +
		"StartAddress(string,表达式), Quantity(string,表达式), DataFormat(枚举), ResultVariable(string,表达式)。";

	public override IStepExecutor CreateExecutor() => new ModbusReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] x{s.Quantity} => {s.ResultVariable}";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusReadPlugin.cs" -Encoding UTF8

@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusWritePlugin : StepPluginBase<ModbusWriteSetting>
{
	public override string StepTypeId => "IO.ModbusWrite";
	public override string DisplayName => "Modbus_Write";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"向 Modbus 设备写入数据，支持线圈和保持寄存器。" +
		"Setting 字段：ConnectionName(string,表达式), SlaveAddress(byte), RegisterType(枚举,Coil/HoldingRegister), " +
		"StartAddress(string,表达式), Values(string,表达式,逗号分隔), DataFormat(枚举)。";

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusWritePlugin.cs" -Encoding UTF8

@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusBatchReadPlugin : StepPluginBase<ModbusBatchReadSetting>
{
	public override string StepTypeId => "IO.ModbusBatchRead";
	public override string DisplayName => "Modbus_BatchRead";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"批量读取多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式。" +
		"Setting 字段：ConnectionName(string,表达式), Items(列表,每项含SlaveAddress/RegisterType/StartAddress/Quantity/DataFormat/ResultVariable), IntervalMs(int)。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusBatchReadPlugin.cs" -Encoding UTF8

@'
using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusBatchWritePlugin : StepPluginBase<ModbusBatchWriteSetting>
{
	public override string StepTypeId => "IO.ModbusBatchWrite";
	public override string DisplayName => "Modbus_BatchWrite";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"批量写入多个 Modbus 地址段。" +
		"Setting 字段：ConnectionName(string,表达式), Items(列表,每项含SlaveAddress/RegisterType/StartAddress/Values/DataFormat), IntervalMs(int)。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin\ModbusBatchWritePlugin.cs" -Encoding UTF8

Write-Host "Modbus Plugin (execution layer) files created successfully!" -ForegroundColor Green
