using CAN.Adapters;
using CAN.Helpers;
using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.XCP;

/// <summary>XCP 执行器公共辅助方法</summary>
internal static class XcpExecutorHelper
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>从 RuntimeData 获取 CAN 适配器并创建 XcpClient</summary>
    public static async Task<(XcpClient? client, string? error)> CreateClientAsync(
        XcpCommonSetting common, IExecutionContext context, CancellationToken ct)
    {
        var connName = await Evaluator.EvalStringAsync(common.ConnectionName, context);
        var key = CanHelper.GetAdapterKey(connName);
        if (!context.CurrentStep!.RuntimeData.TryGetValue(key, out var obj) || obj is not ICanAdapter adapter)
            return (null, $"CAN 连接未找到: {connName}，请先执行 CAN_Open 步骤");

        var txIdStr = await Evaluator.EvalStringAsync(common.TxId, context);
        var rxIdStr = await Evaluator.EvalStringAsync(common.RxId, context);

        uint txId = ParseId(txIdStr);
        uint rxId = ParseId(rxIdStr);

        return (new XcpClient(adapter, txId, rxId, common.TimeoutMs), null);
    }

    public static async Task<string> EvalStringAsync(string expr, IExecutionContext context)
        => await Evaluator.EvalStringAsync(expr, context);

    public static uint ParseId(string idStr)
    {
        idStr = idStr.Trim();
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(idStr[2..], 16);
        return uint.Parse(idStr);
    }

    public static uint ParseAddress(string addrStr)
    {
        addrStr = addrStr.Trim();
        if (addrStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(addrStr[2..], 16);
        return uint.Parse(addrStr);
    }

    public static byte[] ParseHexData(string hexStr)
    {
        hexStr = hexStr.Trim();
        var parts = hexStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }

    public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", " ");
}
