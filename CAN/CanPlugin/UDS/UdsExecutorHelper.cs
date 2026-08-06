using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS;

/// <summary>UDS 执行器公共辅助方法</summary>
internal static class UdsExecutorHelper
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>从 RuntimeData 获取 CAN 适配器并创建 UDS 客户端</summary>
    public static async Task<(UdsClient? client, string? error)> CreateClientAsync(
        UdsCommonSetting common, IExecutionContext context, CancellationToken ct)
    {
        var connName = await Evaluator.EvalStringAsync(common.ConnectionName, context);
        var key = CanHelper.GetAdapterKey(connName);
        if (!context.CurrentStep!.RuntimeData.TryGetValue(key, out var obj) || obj is not ICanAdapter adapter)
            return (null, $"CAN 连接未找到: {connName}");

        var txIdStr = await Evaluator.EvalStringAsync(common.TxId, context);
        var rxIdStr = await Evaluator.EvalStringAsync(common.RxId, context);

        uint txId = ParseId(txIdStr);
        uint rxId = ParseId(rxIdStr);

        var client = new UdsClient(adapter, txId, rxId,
            common.ResponseTimeoutMs, 10000,
            common.FrameType, common.UseFdFrame);

        return (client, null);
    }

    public static uint ParseId(string idStr)
    {
        idStr = idStr.Trim();
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(idStr[2..], 16);
        return uint.Parse(idStr);
    }

    public static string ToHex(byte[] data) =>
        BitConverter.ToString(data).Replace("-", " ");
}
