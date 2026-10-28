using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Ethernet;

internal static class EthernetExecutorHelper
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>字符串表达式的 null 结果为空；不把脚本文本当成实际报文或地址。</summary>
    public static async Task<string> EvalStringAsync(string expr, IExecutionContext context)
        => await Evaluator.EvaluateAsync<string>(expr, context) ?? string.Empty;
}
