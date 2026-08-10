using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Helpers;

internal static class ExpressionHelper
{
    /// <summary>瀵硅〃杈惧紡瀛楁姹傚€煎苟杩斿洖瀛楃涓诧紝姹傚€煎け璐ユ椂鍥為€€鍒板師濮嬭〃杈惧紡瀛楃涓?/summary>
    public static async Task<string> EvalStringAsync(
        this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? expression;
}
