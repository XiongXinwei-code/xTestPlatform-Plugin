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

			context.LogAction?.Invoke($"Modbus 杩炴帴宸插缓绔? {connName} ({setting.TransportType})");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"宸茶繛鎺? {connName}" }
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
					Error = new ErrorInfo { Message = $"Modbus 杩炴帴澶辫触: {ex.Message}" }
				}
			};
		}
	}
}
