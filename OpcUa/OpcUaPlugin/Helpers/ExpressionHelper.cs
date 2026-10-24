using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Helpers;

internal static class ExpressionHelper
{
    /// <summary>字符串表达式按求值结果返回；null 是空值，不将源码误发给服务器。</summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? "";
}
