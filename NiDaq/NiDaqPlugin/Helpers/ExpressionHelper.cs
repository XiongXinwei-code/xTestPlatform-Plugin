using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Helpers;

internal static class ExpressionHelper
{
    /// <summary>返回求值结果；null 为明确空值，不把表达式源码当作设备/任务标识。</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? "";
}
