using OpcUa.Models;
using Opc.Ua;

namespace OpcUa.Helpers;

/// <summary>OPC UA 辅助工具类</summary>
public static class OpcUaHelper
{
    /// <summary>生成运行时数据中的连接 Key</summary>
    public static string GetSessionKey(string connectionName) => $"OpcUa_{connectionName}";

    /// <summary>将字符串值根据指定数据类型转换为对应的 .NET 对象</summary>
    public static object ConvertValue(string value, OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => bool.Parse(value),
            OpcUaDataType.Int16 => short.Parse(value),
            OpcUaDataType.UInt16 => ushort.Parse(value),
            OpcUaDataType.Int32 => int.Parse(value),
            OpcUaDataType.UInt32 => uint.Parse(value),
            OpcUaDataType.Int64 => long.Parse(value),
            OpcUaDataType.UInt64 => ulong.Parse(value),
            OpcUaDataType.Float => float.Parse(value),
            OpcUaDataType.Double => double.Parse(value),
            OpcUaDataType.String => value,
            _ => value // Auto: 保持字符串，由 OPC UA 服务器自动转换
        };
    }

    /// <summary>解析 NodeId 字符串为 OPC UA NodeId 对象</summary>
    public static NodeId ParseNodeId(string nodeIdString)
    {
        return NodeId.Parse(nodeIdString);
    }

    /// <summary>比较值是否满足条件</summary>
    public static bool CompareValue(string actualValue, string expectedValue, OpcUaCompareMode mode)
    {
        return mode switch
        {
            OpcUaCompareMode.Equal => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            OpcUaCompareMode.NotEqual => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            OpcUaCompareMode.GreaterThan => double.TryParse(actualValue, out var a) && double.TryParse(expectedValue, out var b) && a > b,
            OpcUaCompareMode.LessThan => double.TryParse(actualValue, out var a2) && double.TryParse(expectedValue, out var b2) && a2 < b2,
            OpcUaCompareMode.Contains => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
