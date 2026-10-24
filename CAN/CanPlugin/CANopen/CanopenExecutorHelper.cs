using System.Globalization;
using CAN.Adapters;
using CAN.CANopen.Models;
using CAN.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.CANopen;

/// <summary>CANopen 执行器公共辅助方法</summary>
internal static class CanopenExecutorHelper
{
    /// <summary>共享表达式求值器</summary>
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>从资源注册表获取 CAN_Open 创建的适配器</summary>
    public static async Task<(CanopenBus? bus, string? error)> GetBusAsync(CanopenCommonSetting setting, IExecutionContext context, CAN.Runtime.CanExecutionGuard? guard = null)
    {
        var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
        guard?.Check();
        var key = CanHelper.GetAdapterKey(connName);
        if (!context.Resources.TryGet<ICanAdapter>(key, out var adapter))
            return (null, $"CAN 连接未找到: {connName}");
        guard?.Watch(key, adapter);
        return (new CanopenBus(adapter), null);
    }

    /// <summary>对表达式求值并转换为文本；byte[] 转为十六进制字符串</summary>
    public static async Task<string> EvalTextAsync(string expression, IExecutionContext context)
    {
        var value = await Evaluator.EvaluateAsync<object>(expression, context);
        return value switch
        {
            null => throw new InvalidOperationException($"表达式 [{expression}] 的求值结果为 null，不能作为 CANopen 数据"),
            byte[] bytes => ToHex(bytes),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>对表达式求值并解析为数值（支持 0x 前缀）</summary>
    public static async Task<uint> EvalNumberAsync(string expression, IExecutionContext context, string fieldName)
    {
        var text = await EvalTextAsync(expression, context);
        if (!TryParseNumber(text, out var value))
            throw new FormatException($"{fieldName} 无法解析为数值: {text}");
        return value;
    }

    /// <summary>求值并校验节点 ID；允许广播时取值 0~127，否则 1~127</summary>
    public static async Task<byte> EvalNodeIdAsync(string expression, IExecutionContext context, bool allowBroadcast)
    {
        uint node = await EvalNumberAsync(expression, context, "NodeId");
        uint min = allowBroadcast ? 0u : 1u;
        if (node < min || node > 127)
            throw new ArgumentOutOfRangeException(nameof(expression), allowBroadcast
                ? $"NodeId 必须在 0~127 之间（0 表示所有节点），当前值 {node}"
                : $"NodeId 必须在 1~127 之间，当前值 {node}");
        return (byte)node;
    }

    /// <summary>解析十进制或 0x 前缀十六进制数值，自动去除外层引号</summary>
    public static bool TryParseNumber(string text, out uint value)
    {
        text = text.Trim().Trim('"');
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>解析十六进制字节串，允许空格、连字符、逗号和 0x 前缀</summary>
    public static byte[] ParseHex(string text)
    {
        var clean = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "").Replace("-", "").Replace(",", "");
        if (clean.Length % 2 != 0)
            throw new FormatException($"十六进制数据长度必须为偶数: {text}");
        return Convert.FromHexString(clean);
    }

    /// <summary>字节数组转为空格分隔的十六进制字符串</summary>
    public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", " ");

    /// <summary>构造步骤执行结果，结果值统一按不变区域格式化为字符串</summary>
    public static ExecutionResult Result(TestStatus status, object? value = null, string? error = null)
    {
        var text = value switch
        {
            null => null,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        var result = status switch
        {
            TestStatus.Completed => ExecutionResults.Completed(text),
            TestStatus.Aborted => ExecutionResults.Aborted(text),
            TestStatus.Error => ExecutionResults.Error(error ?? "CANopen 执行失败，未提供错误描述", value: text),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "不支持的 CANopen 执行状态")
        };
        result.StepResult.Value = text ?? string.Empty;
        return result;
    }

    /// <summary>异常映射：用户取消 → Aborted；SDO 中止、超时及其他异常 → Error</summary>
    public static ExecutionResult FromException(Exception ex, CancellationToken ct, string? operation = null) => ex switch
    {
        OperationCanceledException when ct.IsCancellationRequested => ExecutionResults.Aborted(),
        CanopenSdoAbortException abort => ExecutionResults.FromException(abort,
            $"{operation ?? "CANopen SDO 操作"}失败：{abort.Message}", value: $"0x{abort.AbortCode:X8}"),
        _ => ExecutionResults.FromException(ex, $"{operation ?? "CANopen 操作"}失败：{ex.Message}")
    };
}
