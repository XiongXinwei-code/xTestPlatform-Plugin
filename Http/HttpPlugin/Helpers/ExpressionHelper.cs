using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Helpers;

internal static class ExpressionHelper
{
    /// <summary>对表达式字段求值并返回字符串，求值失败时回退到原始表达式字符串</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? expression;

    /// <summary>对表达式字段求值并返回整数，求值失败或结果非法时返回默认值</summary>
    public static async Task<int> EvalIntAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context, int fallback)
    {
        if (string.IsNullOrWhiteSpace(expression)) return fallback;
        var text = await evaluator.EvalStringAsync(expression, context);
        return int.TryParse(text, out var value) ? value : fallback;
    }
}
