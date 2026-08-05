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
			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
				var key = ModbusHelper.GetConnectionKey(connName);

				// 若已存在同名连接（序列异常终止未断开），先销毁旧资源
				if (context.CurrentStep.RuntimeData.TryGetValue(key, out var existingMaster) && existingMaster is IModbusMaster oldMaster)
				{
					try { oldMaster.Dispose(); } catch { /* 忽略销毁异常 */ }
				}
				if (context.CurrentStep.RuntimeData.TryGetValue(key + "_transport", out var existingTransport) && existingTransport is IDisposable oldTransport)
				{
					try { oldTransport.Dispose(); } catch { /* 忽略销毁异常 */ }
				}
				if (existingMaster != null || existingTransport != null)
					context.LogAction?.Invoke($"Modbus 连接 {connName} 检测到已有连接，已自动销毁旧连接");

				IModbusMaster master;
			object transport;
			var factory = new ModbusFactory();

			if (setting.TransportType == ModbusTransportType.TCP)
			{
				var ip = await Evaluator.EvalStringAsync(setting.IpAddress, context);
				var client = new TcpClient();
				await client.ConnectAsync(ip, setting.TcpPort, cancellationToken);
				client.ReceiveTimeout = setting.TimeoutMs;
				client.SendTimeout = setting.TimeoutMs;
				master = factory.CreateMaster(client);
				transport = client;
			}
			else
			{
				var portName = await Evaluator.EvalStringAsync(setting.PortName, context);
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