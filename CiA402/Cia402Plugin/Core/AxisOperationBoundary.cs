namespace CiA402.Core;

/// <summary>仅属于当前轴动作的提交/结果边界，不存入长生命周期会话。</summary>
internal static class AxisOperationBoundary
{
    private static readonly AsyncLocal<Action?> Current = new();

    public static IDisposable Enter(Action check)
    {
        var previous = Current.Value;
        Current.Value = check;
        return new Scope(previous);
    }

    public static void Check() => Current.Value?.Invoke();

    private sealed class Scope(Action? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
