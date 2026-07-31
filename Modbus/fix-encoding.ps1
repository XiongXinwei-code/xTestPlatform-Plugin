# Fix Chinese encoding - rewrite files with correct UTF-8
$modbusRoot = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

# ModbusConnectPlugin.cs
$content = @"
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
		"Build Modbus connection, supports TCP and RTU(Serial) transport." +
		"Setting: ConnectionName(string,expr), TransportType(enum,TCP/RTU), " +
		"IpAddress(string,expr,TCP addr), TcpPort(int), " +
		"PortName(string,expr,COM port), BaudRate(int), DataBits(int), StopBits(int), Parity(int), TimeoutMs(int).";

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? `$"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: `$"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusConnectPlugin.cs", $content, $utf8)

# ModbusDisconnectPlugin.cs
$content = @"
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
		"Close the specified Modbus connection." +
		"Setting: ConnectionName(string,expr).";

	public override IStepExecutor CreateExecutor() => new ModbusDisconnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return `$"Disconnect {s.ConnectionName}";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusDisconnectPlugin.cs", $content, $utf8)

# ModbusReadPlugin.cs
$content = @"
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
		"Read data from Modbus device. Supports Coil, DiscreteInput, HoldingRegister, InputRegister." +
		"Setting: ConnectionName(string,expr), SlaveAddress(byte), RegisterType(enum), " +
		"StartAddress(string,expr), Quantity(string,expr), DataFormat(enum), ResultVariable(string,expr).";

	public override IStepExecutor CreateExecutor() => new ModbusReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return `$"Read {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] x{s.Quantity} => {s.ResultVariable}";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusReadPlugin.cs", $content, $utf8)

# ModbusWritePlugin.cs
$content = @"
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
		"Write data to Modbus device. Supports Coil and HoldingRegister." +
		"Setting: ConnectionName(string,expr), SlaveAddress(byte), RegisterType(enum,Coil/HoldingRegister), " +
		"StartAddress(string,expr), Values(string,expr,comma-separated), DataFormat(enum).";

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return `$"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusWritePlugin.cs", $content, $utf8)

# ModbusBatchReadPlugin.cs
$content = @"
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
		"Batch read multiple Modbus address ranges. Each item can specify different slave, register type and data format." +
		"Setting: ConnectionName(string,expr), Items(list), IntervalMs(int).";

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return `$"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusBatchReadPlugin.cs", $content, $utf8)

# ModbusBatchWritePlugin.cs
$content = @"
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
		"Batch write to multiple Modbus address ranges." +
		"Setting: ConnectionName(string,expr), Items(list), IntervalMs(int).";

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return `$"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\ModbusBatchWritePlugin.cs", $content, $utf8)

# Fix Executors with Chinese log messages
$execDir = "$modbusRoot\ModbusPlugin\Executors"

# ModbusConnectExecutor.cs
$content = @"
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

			context.LogAction?.Invoke(`$"Modbus connected: {connName} ({setting.TransportType})");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = `$"Connected: {connName}" }
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
					Error = new ErrorInfo { Message = `$"Modbus connect failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusConnectExecutor.cs", $content, $utf8)

# ModbusDisconnectExecutor.cs
$content = @"
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

			context.LogAction?.Invoke(`$"Modbus disconnected: {connName}");
			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = `$"Disconnected: {connName}" }
			};
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = `$"Modbus disconnect failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusDisconnectExecutor.cs", $content, $utf8)

# ModbusReadExecutor.cs
$content = @"
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
						Error = new ErrorInfo { Message = `$"Modbus connection not found: {connName}" }
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

			context.LogAction?.Invoke(`$"Modbus Read: Addr={startAddr}, Qty={quantity}, Result={result}");

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
					Error = new ErrorInfo { Message = `$"Modbus read failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusReadExecutor.cs", $content, $utf8)

# ModbusWriteExecutor.cs
$content = @"
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
						Error = new ErrorInfo { Message = `$"Modbus connection not found: {connName}" }
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

			context.LogAction?.Invoke(`$"Modbus Write: Addr={startAddr}, Values={valuesStr}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = `$"Written: Addr={startAddr}" }
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
					Error = new ErrorInfo { Message = `$"Modbus write failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusWriteExecutor.cs", $content, $utf8)

# ModbusBatchReadExecutor.cs
$content = @"
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
						Error = new ErrorInfo { Message = `$"Modbus connection not found: {connName}" }
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

				results.Add(`$"{item.ResultVariable}={result}");

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			var summary = string.Join("; ", results);
			context.LogAction?.Invoke(`$"Modbus BatchRead: {summary}");

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
					Error = new ErrorInfo { Message = `$"Modbus batch read failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusBatchReadExecutor.cs", $content, $utf8)

# ModbusBatchWriteExecutor.cs
$content = @"
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
						Error = new ErrorInfo { Message = `$"Modbus connection not found: {connName}" }
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

			context.LogAction?.Invoke(`$"Modbus BatchWrite: {setting.Items.Count} items written");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = `$"Written {setting.Items.Count} items" }
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
					Error = new ErrorInfo { Message = `$"Modbus batch write failed: {ex.Message}" }
				}
			};
		}
	}
}
"@
[IO.File]::WriteAllText("$execDir\ModbusBatchWriteExecutor.cs", $content, $utf8)

# Fix Models with Chinese XML comments
$modelsDir = "$modbusRoot\ModbusPlugin\Models"

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusConnectSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	public ModbusTransportType TransportType { get; set; } = ModbusTransportType.TCP;

	[ExpressionField]
	public string IpAddress { get; set; } = "192.168.1.1";

	public int TcpPort { get; set; } = 502;

	[ExpressionField]
	public string PortName { get; set; } = "COM1";

	public int BaudRate { get; set; } = 9600;

	public int DataBits { get; set; } = 8;

	public int StopBits { get; set; } = 1;

	public int Parity { get; set; } = 0;

	public int TimeoutMs { get; set; } = 3000;
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusConnectSetting.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusDisconnectSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusDisconnectSetting.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusReadSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	public byte SlaveAddress { get; set; } = 1;

	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	[ExpressionField]
	public string Quantity { get; set; } = "1";

	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	[ExpressionField]
	public string ResultVariable { get; set; } = "ModbusResult";
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusReadSetting.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusWriteSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	public byte SlaveAddress { get; set; } = 1;

	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	[ExpressionField]
	public string StartAddress { get; set; } = "0";

	[ExpressionField]
	public string Values { get; set; } = "0";

	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusWriteSetting.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchItem
{
	public byte SlaveAddress { get; set; } = 1;

	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	public ushort StartAddress { get; set; } = 0;

	public ushort Quantity { get; set; } = 1;

	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;

	public string ResultVariable { get; set; } = "";
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusBatchItem.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchReadSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	public List<ModbusBatchItem> Items { get; set; } = new();

	public int IntervalMs { get; set; } = 0;
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusBatchReadSetting.cs", $content, $utf8)

$content = @"
using MessagePack;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteItem
{
	public byte SlaveAddress { get; set; } = 1;

	public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

	public ushort StartAddress { get; set; } = 0;

	public string Values { get; set; } = "0";

	public ModbusDataFormat DataFormat { get; set; } = ModbusDataFormat.UInt16;
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusBatchWriteItem.cs", $content, $utf8)

$content = @"
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteSetting
{
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	public List<ModbusBatchWriteItem> Items { get; set; } = new();

	public int IntervalMs { get; set; } = 0;
}
"@
[IO.File]::WriteAllText("$modelsDir\ModbusBatchWriteSetting.cs", $content, $utf8)

# Helpers
$content = @"
namespace Modbus.Helpers;

public static class ModbusHelper
{
	public static string GetConnectionKey(string connectionName) => `$"__Modbus_{connectionName}";
}
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\Helpers\ModbusHelper.cs", $content, $utf8)

$content = @"
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
"@
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin\Helpers\ModbusDataConverter.cs", $content, $utf8)

# ModbusEnums.cs
$content = @"
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
"@
[IO.File]::WriteAllText("$modelsDir\ModbusEnums.cs", $content, $utf8)

Write-Host "All files re-written with correct UTF-8 encoding!" -ForegroundColor Green
