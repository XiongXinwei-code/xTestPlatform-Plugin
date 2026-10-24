using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Helpers;

internal static class ExpressionHelper
{
    /// <summary>空配置保留为空；表达式结果为 null 时明确报错，不把源码当作设备参数。</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => string.IsNullOrWhiteSpace(expression) ? string.Empty
            : await evaluator.EvaluateAsync<string>(expression, context)
                ?? throw new InvalidOperationException($"LIN 表达式 {expression} 的结果为 null，请检查变量值或使用字符串表达式");
}
