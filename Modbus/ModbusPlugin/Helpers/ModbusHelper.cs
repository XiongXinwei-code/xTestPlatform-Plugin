using xTestPlatform.Core.Engine;
using Modbus.Models;
using System.Net;

namespace Modbus.Helpers;

/// <summary>
/// Modbus 通用辅助方法
/// </summary>
public static class ModbusHelper
{
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> RegistryLocks = new();
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, long>> CloseGenerations = new();
	public static object RegistryLock(object resources) => RegistryLocks.GetValue(resources, static _ => new object());
	internal static long GetCloseGeneration(object resources, string key)
	{
		lock (RegistryLock(resources)) return CloseGenerations.GetOrCreateValue(resources).GetValueOrDefault(key);
	}
	internal static void MarkCloseRequested(object resources, string key)
	{
		lock (RegistryLock(resources))
		{
			var generations = CloseGenerations.GetOrCreateValue(resources);
			generations[key] = generations.GetValueOrDefault(key) + 1;
		}
	}
	internal static void ThrowIfOpenSuperseded(object resources, string key, long generation)
	{
		if (GetCloseGeneration(resources, key) != generation)
			throw new InvalidOperationException("Modbus 建立期间收到同名断开请求，旧代次不得迟到登记");
	}
	/// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
	public static string GetConnectionKey(string connectionName) => $"__Modbus_{connectionName}";

    public static string GetPhysicalResourceKey(ModbusTransportType type, string target, int port = 502)
    {
        var name = target.Trim();
        if (name.Length == 0) throw new ArgumentException("Modbus 物理连接目标不能为空");
        if (type == ModbusTransportType.RTU)
        {
            name = name.TrimEnd(':');
            if (name.StartsWith(@"\\.\", StringComparison.Ordinal)) name = name[4..];
            if (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(3), out int number)) name = "COM" + number;
            return "Modbus:RTU:" + name.ToUpperInvariant();
        }
        if (type != ModbusTransportType.TCP) throw new ArgumentOutOfRangeException(nameof(type));
        if (port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (IPAddress.TryParse(name.Trim('[', ']'), out var address)) name = address.ToString();
        return $"Modbus:TCP:{name.ToUpperInvariant()}:{port}";
    }
    public static int RemainingBudget(long startedAt, int timeoutMs)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        double remaining = timeoutMs - System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException("Modbus 建立/关闭总期限已到，不另加固定收尾等待");
        return (int)Math.Ceiling(remaining);
    }

	/// <summary>连接超时值的存储键，供后续读写步骤复用连接阶段配置的超时</summary>
	public static string GetTimeoutKey(string connectionName) => $"__Modbus_{connectionName}_timeout";

	/// <summary>默认超时时间（毫秒），用于兼容未保存超时值的旧序列</summary>
	public const int DefaultTimeoutMs = 3000;

	/// <summary>读取连接阶段保存的超时值；未找到或非法时返回默认值</summary>
	public static int ResolveTimeoutMs(IExecutionContext context, string connectionName)
	{
		if (context.Resources.TryGet<ModbusConnectionResource>(GetConnectionKey(connectionName), out var resource))
			return resource.TimeoutMs;
		if (context.Resources.TryGet<object>(GetTimeoutKey(connectionName), out var boxed)
			&& boxed is int timeout && timeout > 0)
			return timeout;
		return DefaultTimeoutMs;
	}

}
