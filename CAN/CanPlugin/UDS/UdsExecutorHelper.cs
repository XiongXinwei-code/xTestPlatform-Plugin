using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS;

/// <summary>UDS 执行器公共辅助方法</summary>
internal static class UdsExecutorHelper
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>从资源注册表获取 CAN 适配器并创建 UDS 客户端</summary>
    public static async Task<(UdsClient? client, string? error)> CreateClientAsync(
        UdsCommonSetting common, IExecutionContext context, CancellationToken ct, CAN.Runtime.CanExecutionGuard? guard = null)
    {
        if (common is UdsDiagSessionSetting session && !Enum.IsDefined(session.SessionType))
            return (null, "未知的诊断会话类型，未发送请求；自定义子功能请使用 UDS.RawRequest");
        if (common is UdsRoutineControlSetting routine && !Enum.IsDefined(routine.ControlType))
            return (null, "未知的例程控制类型，未发送请求；自定义子功能请使用 UDS.RawRequest");
        if (common.TransactionTimeoutMs <= 0 || common.ResponseTimeoutMs == 0 || common.ResponseTimeoutMs < -1)
            return (null, "UDS 事务总超时必须大于 0；响应超时必须大于 0 或为 -1（仍受事务总期限约束）");
        if (common.MaxResponseBytes < 1)
            return (null, "最大响应字节数 MaxResponseBytes 必须大于 0；默认 4194304（4 MiB）");
        var connName = await Evaluator.EvalStringAsync(common.ConnectionName, context);
        guard?.Check();
        var key = CanHelper.GetAdapterKey(connName);
        if (!context.Resources.TryGet<ICanAdapter>(key, out var adapter))
        {
            guard?.Check();
            return (null, $"CAN 连接未找到: {connName}");
        }
        guard?.Watch(key, adapter);

        var txIdStr = await Evaluator.EvalStringAsync(common.TxId, context);
        var rxIdStr = await Evaluator.EvalStringAsync(common.RxId, context);
        guard?.Check();

        uint txId = ParseId(txIdStr);
        uint rxId = ParseId(rxIdStr);

        var client = new UdsClient(adapter, txId, rxId,
            common.ResponseTimeoutMs, 10000,
            common.FrameType, common.UseFdFrame, common.MaxResponseBytes, common.TransactionTimeoutMs);

        return (client, null);
    }

    public static uint ParseId(string idStr)
    {
        idStr = idStr.Trim();
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(idStr[2..], 16);
        return uint.Parse(idStr);
    }

    public static uint ParseBoundedId(string text, uint maximum, string field)
    {
        uint value = ParseId(text);
        if (value > maximum)
            throw new ArgumentOutOfRangeException(field, $"{field} 必须在 0~0x{maximum:X} 范围内，实际 0x{value:X}；未发送请求，不能截断到另一标识");
        return value;
    }

    public static string ToHex(byte[] data) =>
        BitConverter.ToString(data).Replace("-", " ");

    public static ExecutionResult Failure(string operation, UdsResponse response, string value = "")
        => ExecutionResults.Error($"{operation}失败：{response.GetNrcDescription()}",
            detail: $"{response.RequestDescription}；{response.GetFailureValue()}", value: value);
}
