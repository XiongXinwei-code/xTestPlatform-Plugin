using System.Threading;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Executors;

/// <summary>
/// 娴嬭瘯妗╋細鍏佽鍗曞厓娴嬭瘯娉ㄥ叆 Mock IExpressionEvaluator銆?/// </summary>
public static class TestableEvaluator
{
    [ThreadStatic]
    private static IExpressionEvaluator? _current;

    public static IExpressionEvaluator? Current => _current;

    public static IDisposable Use(IExpressionEvaluator evaluator) => new Scope(evaluator);

    private sealed class Scope : IDisposable
    {
        private readonly IExpressionEvaluator? _previous;

        public Scope(IExpressionEvaluator evaluator)
        {
            _previous = _current;
            _current = evaluator;
        }

        public void Dispose() => _current = _previous;
    }
}
