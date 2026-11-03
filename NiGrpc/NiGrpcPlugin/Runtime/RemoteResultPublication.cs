namespace NiGrpc.Runtime;

internal static class RemoteResultPublication
{
    internal static void Publish(string method, string remoteName, Action check, Action publish)
    {
        try { check(); publish(); check(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"NI {method} 已收到服务端成功响应，但结果发布未确认；远端名称={remoteName}；输出可能已写入，不能盲目重试", ex);
        }
    }
}
