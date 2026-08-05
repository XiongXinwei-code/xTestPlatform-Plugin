using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace SerialPort.Helpers;

internal static class ExpressionHelper
{
    /// <summary>对表达式字段求值并返回字符串，求值失败时回退到原始表达式字符串</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? expression;
}
