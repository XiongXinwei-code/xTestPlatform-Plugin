using CAN.Models;

namespace CAN.Helpers;

/// <summary>CAN 连接管理辅助类，通过 RuntimeData 存取适配器实例</summary>
public static class CanHelper
{
    private const string KeyPrefix = "CAN_Adapter_";

    public static string GetAdapterKey(string connectionName) => $"{KeyPrefix}{connectionName}";
}
