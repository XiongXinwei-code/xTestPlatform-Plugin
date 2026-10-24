using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Helpers;

internal static class ExpressionHelper
{
    /// <summary>空配置保留为空；实际 null 不得回退为源码并作为连接名或设备数据。</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => string.IsNullOrWhiteSpace(expression) ? string.Empty :
            await evaluator.EvaluateAsync<string>(expression, context)
            ?? throw new InvalidOperationException($"表达式 [{expression}] 的求值结果为 null，不能作为 CAN 配置或发送数据");
}
