using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Ethernet;

internal static class EthernetExecutorHelper
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>对表达式字段求值并返回字符串，求值失败时回退到原始表达式字符串。</summary>
    public static async Task<string> EvalStringAsync(string expr, IExecutionContext context)
        => await Evaluator.EvaluateAsync<string>(expr, context) ?? expr;
}
