using xTestPlatform.Core.Engine;

namespace Modbus.Helpers;

/// <summary>
/// Modbus 通用辅助方法
/// </summary>
public static class ModbusHelper
{
	/// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
	public static string GetConnectionKey(string connectionName) => $"__Modbus_{connectionName}";

	/// <summary>连接超时值的存储键，供后续读写步骤复用连接阶段配置的超时</summary>
	public static string GetTimeoutKey(string connectionName) => $"__Modbus_{connectionName}_timeout";

	/// <summary>默认超时时间（毫秒），用于兼容未保存超时值的旧序列</summary>
	public const int DefaultTimeoutMs = 3000;

	/// <summary>读取连接阶段保存的超时值；未找到或非法时返回默认值</summary>
	public static int ResolveTimeoutMs(IExecutionContext context, string connectionName)
	{
		if (context.Resources.TryGet<object>(GetTimeoutKey(connectionName), out var boxed)
			&& boxed is int timeout && timeout > 0)
			return timeout;
		return DefaultTimeoutMs;
	}

	/// <summary>
	/// 为 NModbus 调用附加软超时。
	/// NModbus 的 *Async 方法不接受 CancellationToken，且 Transport.ReadTimeout 对异步路径不一定生效，
	/// 从站无响应时会永久挂起整条序列；此处用 WaitAsync 保证步骤能按时返回并报错。
	/// 超时抛出 <see cref="TimeoutException"/>，用户取消抛出 <see cref="OperationCanceledException"/>。
	/// </summary>
	public static async Task<T> WithTimeoutAsync<T>(
		Task<T> operation, int timeoutMs, string description, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await operation.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
		}
		catch (TimeoutException)
		{
			// 底层调用仍在后台运行，标记其异常避免 UnobservedTaskException
			_ = operation.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
			throw new TimeoutException($"{description}超时({timeoutMs}ms): 从站无响应");
		}
	}

	/// <summary>无返回值版本的软超时包装</summary>
	public static async Task WithTimeoutAsync(
		Task operation, int timeoutMs, string description, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await operation.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
		}
		catch (TimeoutException)
		{
			_ = operation.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
			throw new TimeoutException($"{description}超时({timeoutMs}ms): 从站无响应");
		}
	}
}