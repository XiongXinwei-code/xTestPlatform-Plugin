namespace UdpCommunication.Helpers;

/// <summary>
/// UDP 连接键生成器。
/// 注：运行时连接归属使用 *Open 步骤的 StepAddress* 作为键（而非 LocalAddress:LocalPort），
/// 这样后续 Send/Receive/SendAndReceive/Close 步骤可以在不重复填写 IP/端口的前提下
/// 唯一引用某个 Open 步骤创建的 transport。
/// </summary>
public static class UdpHelper
{
    private const string ConnectionKeyPrefix = "__UDP_";

    public static string GetConnectionKey(string stepAddress)
        => $"{ConnectionKeyPrefix}{stepAddress}";

    public static string? ExtractStepAddress(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(ConnectionKeyPrefix))
        {
            return null;
        }

        return key[ConnectionKeyPrefix.Length..];
    }
}
