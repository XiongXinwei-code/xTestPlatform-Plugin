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

/// <summary>
/// Modbus 连接执行器，建立 TCP 或 RTU 连接并将 IModbusMaster 存入运行时数据
/// </summary>
public sealed class ModbusConnectExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 连接操作</summary>

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
					master = factory.CreateRtuMaster(new SerialPortAdapter(port));
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