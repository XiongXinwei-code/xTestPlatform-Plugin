using System.Globalization;
using System.Security.Cryptography;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Helpers;

/// <summary>在步骤准备阶段解析 Data ID 表；周期任务仅使用返回的冻结配置，不保留表达式上下文。</summary>
public static class CanPayloadProtectionExpressions
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public static bool UsesTableExpression(CanPayloadProtectionSetting? setting) =>
        setting is { CrcEnabled: true, CrcDataIdEnabled: true, CrcDataIdMode: CanCrcDataIdMode.Profile2, CrcDataIdTableUseExpression: true };

    /// <summary>只为静态字段布局校验提供占位表；不可作为运行时表达式失败的回退。</summary>
    public static CanPayloadProtectionSetting? ForLayoutValidation(CanPayloadProtectionSetting? setting)
    {
        var copy = setting?.Copy();
        if (UsesTableExpression(copy))
        {
            copy!.CrcDataIdTable = Enumerable.Repeat("00", 16).ToArray();
            copy.CrcDataIdTableUseExpression = false;
        }
        return copy;
    }

    public static async Task<CanPayloadProtectionSetting> ResolveAsync(CanPayloadProtectionSetting? setting,
        IExecutionContext context, CancellationToken ct, Action check, Action<string>? log = null, Action<Task>? track = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(check);
        void Check() { ct.ThrowIfCancellationRequested(); check(); ct.ThrowIfCancellationRequested(); }

        Check();
        var copy = setting?.Copy() ?? new();
        if (!UsesTableExpression(copy)) return copy;
        string expression = copy.CrcDataIdTableExpression;
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Profile 2 Data ID 表表达式不能为空，求值结果必须是恰好 16 项的 byte[]");

        // The SDK evaluator has no cancellation parameter and may execute synchronously
        // before returning its Task. Keep it off the calling thread; cancellation ends
        // only our wait and never authorizes a late result to enter a sending job.
        var evaluation = Task.Run(async () =>
        {
            Check();
            object? result = await Evaluator.EvaluateAsync<object>(expression, context).ConfigureAwait(false);
            byte[]? frozen = result is byte[] { Length: 16 } bytes ? (byte[])bytes.Clone() : null;
            Check();
            if (frozen == null)
                throw new ArgumentException("Profile 2 Data ID 表表达式必须返回恰好 16 项的 byte[]，不接受空值、字符串或其他数值数组");
            return frozen;
        }, CancellationToken.None);

        // Ownership tracks the actual evaluator, not the shorter cancellable wait.
        // Register fault observation before invoking an external tracker that may throw.
        _ = evaluation.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        track?.Invoke(evaluation);

        byte[] table = await evaluation.WaitAsync(ct).ConfigureAwait(false);
        Check();
        copy.CrcDataIdTable = table.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)).ToArray();
        copy.CrcDataIdTableUseExpression = false;
        string snapshot = string.Join(" ", copy.CrcDataIdTable);
        string digest = Convert.ToHexString(SHA256.HashData(table));
        Check();
        log?.Invoke($"Profile 2 Data ID 表已解析并冻结：[{snapshot}]；SHA-256={digest}");
        Check();
        return copy;
    }
}
