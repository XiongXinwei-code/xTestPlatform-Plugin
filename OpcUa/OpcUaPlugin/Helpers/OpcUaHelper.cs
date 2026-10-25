using OpcUa.Models;
using Opc.Ua;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace OpcUa.Helpers;

/// <summary>OPC UA 辅助工具类</summary>
public static class OpcUaHelper
{
    internal static void EnsureCurrentConnection(xTestPlatform.Core.Engine.IExecutionContext context, string key,
        OpcUaConnectionResource expected, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        bool found = context.Resources.TryGet<OpcUaConnectionResource>(key, out var current);
        caller.ThrowIfCancellationRequested();
        if (!found || !ReferenceEquals(current, expected))
            throw new InvalidOperationException("OPC UA 连接资源代次已改变，禁止发布旧连接结果");
        caller.ThrowIfCancellationRequested();
        expected.EnsureAvailableForPublication();
    }
    private sealed class RegistrationState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public long CloseGeneration;
    }
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, RegistrationState>> RegistrationLocks = new();
    private static RegistrationState Registration(object registry, string key) =>
        RegistrationLocks.GetValue(registry, _ => new()).GetOrAdd(key, _ => new());
    public static SemaphoreSlim GetRegistrationLock(object registry, string key) =>
        Registration(registry, key).Gate;
    public static long GetCloseGeneration(object registry, string key) => Interlocked.Read(ref Registration(registry, key).CloseGeneration);
    public static void MarkCloseRequested(object registry, string key) => Interlocked.Increment(ref Registration(registry, key).CloseGeneration);
    public static void ThrowIfOpenSuperseded(object registry, string key, long generation)
    {
        if (GetCloseGeneration(registry, key) != generation)
            throw new InvalidOperationException("OPC UA 建立期间收到同名关闭请求，旧代次不得迟到报告可用");
    }
    public static int RemainingBudget(long startedAt, int timeoutMs)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        double remaining = timeoutMs - Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException($"OPC UA 建立/关闭事务总期限 {timeoutMs} ms 已到");
        return (int)Math.Ceiling(remaining);
    }

    /// <summary>条件判断仅使用非空且 Good 的值；Bad/Uncertain 不是可用的完成证据。</summary>
    public static bool CompareValue(DataValue? value, string expectedValue, OpcUaCompareMode mode) =>
        value?.Value != null && StatusCode.IsGood(value.StatusCode) &&
        CompareValue(Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? "", expectedValue, mode);

    /// <summary>生成运行时数据中的连接 Key</summary>
    public static string GetSessionKey(string connectionName) => $"OpcUa_{connectionName}";

    /// <summary>仅等待同 endpoint、同逻辑名的已退休连接；不独占服务器，不触及其他活动会话。</summary>
    public static string GetRetirementKey(string endpointUrl, string connectionName) =>
        $"OPC:{new Uri(endpointUrl.Trim(), UriKind.Absolute).AbsoluteUri}:{connectionName}";

    /// <summary>将字符串值根据指定数据类型转换为对应的 .NET 对象</summary>
    public static object ConvertValue(string value, OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => bool.Parse(value),
            OpcUaDataType.Int16 => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.UInt16 => ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.Int32 => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.UInt32 => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.Int64 => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.UInt64 => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            OpcUaDataType.Float => ParseFiniteFloat(value),
            OpcUaDataType.Double => ParseFiniteDouble(value),
            OpcUaDataType.String => value,
            OpcUaDataType.Auto => value, // 保留辅助 API 的旧 Auto 字符串语义；步骤在读取实际类型后转换。
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), "未知 OPC UA 数据类型，禁止字符串回退")
        };
    }

    private static float ParseFiniteFloat(string value)
    {
        var number = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return float.IsFinite(number) ? number : throw new FormatException("OPC UA 浮点输入必须是有限数值，不接受 NaN/Infinity");
    }
    internal static object ConvertAutoValue(string value, Type runtimeType)
    {
        if (runtimeType == typeof(float)) return ParseFiniteFloat(value);
        if (runtimeType == typeof(double)) return ParseFiniteDouble(value);
        if (runtimeType == typeof(decimal)) return decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, runtimeType, CultureInfo.InvariantCulture);
    }
    private static double ParseFiniteDouble(string value)
    {
        var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return double.IsFinite(number) ? number : throw new FormatException("OPC UA 浮点输入必须是有限数值，不接受 NaN/Infinity");
    }
    private static bool TryFiniteNumber(string text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    /// <summary>解析 NodeId 字符串为 OPC UA NodeId 对象</summary>
    public static NodeId ParseNodeId(string nodeIdString)
    {
        return NodeId.Parse(nodeIdString);
    }

    /// <summary>校验 NodeId 字符串格式是否合法（如 ns=2;s=MyVariable）</summary>
    public static bool IsValidNodeId(string nodeIdString)
    {
        if (string.IsNullOrWhiteSpace(nodeIdString)) return false;
        try
        {
            var nodeId = NodeId.Parse(nodeIdString);
            return !NodeId.IsNull(nodeId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>比较值是否满足条件</summary>
    public static bool CompareValue(string actualValue, string expectedValue, OpcUaCompareMode mode)
    {
        return mode switch
        {
            OpcUaCompareMode.Equal => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            OpcUaCompareMode.NotEqual => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            OpcUaCompareMode.GreaterThan => TryFiniteNumber(actualValue, out var a) && TryFiniteNumber(expectedValue, out var b) && a > b,
            OpcUaCompareMode.LessThan => TryFiniteNumber(actualValue, out var a2) && TryFiniteNumber(expectedValue, out var b2) && a2 < b2,
            OpcUaCompareMode.Contains => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
