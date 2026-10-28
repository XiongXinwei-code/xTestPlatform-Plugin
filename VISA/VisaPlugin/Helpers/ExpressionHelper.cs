using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Helpers;

internal static class ExpressionHelper
{
    /// <summary>求值失败按异常传播；null 结果保持为空，不把表达式源码误当协议数据。</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? string.Empty;
}
