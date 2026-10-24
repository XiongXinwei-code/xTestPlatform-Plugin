using System.IO.Ports;
using System.Net.Sockets;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using PluginCommon;
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
	private readonly Func<ModbusConnectionResource, ModbusConnectSetting, string, CancellationToken, Task> _open;
	public ModbusConnectExecutor() : this(OpenNativeAsync) { }
	internal ModbusConnectExecutor(Func<ModbusConnectionResource, ModbusConnectSetting, string, CancellationToken, Task> open) => _open = open;

	/// <summary>执行 Modbus 连接操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
        ModbusConnectionResource? opening = null;
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusConnectPlugin().CreateSerializer();
			var setting = (ModbusConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			epoch.ThrowIfExpired();
			var key = ModbusHelper.GetConnectionKey(connName);
			if (string.IsNullOrWhiteSpace(connName)) throw new ArgumentException("连接名称不能为空");
			if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
			if (!Enum.IsDefined(setting.TransportType)) throw new ArgumentOutOfRangeException(nameof(setting.TransportType));
			cancellationToken.ThrowIfCancellationRequested();
			var physicalTarget = await Evaluator.EvalStringAsync(setting.TransportType == ModbusTransportType.TCP ? setting.IpAddress : setting.PortName, context);
			epoch.ThrowIfExpired();
			cancellationToken.ThrowIfCancellationRequested();
			var physicalKey = ModbusHelper.GetPhysicalResourceKey(setting.TransportType, physicalTarget, setting.TcpPort);

			var resource = new ModbusConnectionResource(setting.TimeoutMs, ex => context.Log(LogLevel.Error, $"Modbus {connName} 收尾未确认", ex.ToString()), physicalKey);
			opening = resource;
			await ModbusLifecycle.OpenRegisteredAsync(context, connName, resource,
				(owner, token) => _open(owner, setting, physicalTarget, token), cancellationToken, epoch.ThrowIfExpired);
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, resource, cancellationToken);

			context.Log($"Modbus 连接已建立: {connName} ({setting.TransportType})");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, resource, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已连接: {connName}" }
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			opening?.Dispose();
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			try { opening?.Dispose(); }
			catch (Exception cleanup) { ex = new AggregateException("Modbus 建立失败且本次资源收尾未确认", ex, cleanup); }
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = ErrorInfo.FromException(ex, $"Modbus 连接失败: {ex.Message}")
				}
			};
		}
	}

	private static async Task OpenNativeAsync(ModbusConnectionResource owner, ModbusConnectSetting setting, string physicalTarget, CancellationToken token)
	{
		var factory = new ModbusFactory();
		IModbusMaster master;
		if (setting.TransportType == ModbusTransportType.TCP)
		{
			token.ThrowIfCancellationRequested();
			var client = new TcpClient(); owner.OwnTransport(client);
			await client.ConnectAsync(physicalTarget, setting.TcpPort, token);
			token.ThrowIfCancellationRequested();
			var stream = new CancellableTcpStream(client, setting.TimeoutMs); owner.OwnTransport(stream);
			master = factory.CreateIpMaster(stream);
		}
		else
		{
			token.ThrowIfCancellationRequested();
			var port = new SerialPort(physicalTarget, setting.BaudRate, (Parity)setting.Parity, setting.DataBits, (StopBits)setting.StopBits);
			owner.OwnTransport(port);
			port.ReadTimeout = port.WriteTimeout = setting.TimeoutMs;
			port.Open(); token.ThrowIfCancellationRequested();
			master = factory.CreateRtuMaster(new SerialPortAdapter(port));
		}
		owner.OwnMaster(master);
		master.Transport.ReadTimeout = master.Transport.WriteTimeout = setting.TimeoutMs;
	}
}
