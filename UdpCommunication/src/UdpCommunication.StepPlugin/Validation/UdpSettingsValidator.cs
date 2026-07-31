using System.Net;
using UdpCommunication.StepPlugin.Transport;

namespace UdpCommunication.StepPlugin.Validation;

public static class UdpSettingsValidator
{
    public static string? ValidateEndpoint(UdpEndpointOptions endpoint)
    {
        if (!IsIpv4(endpoint.LocalAddress) || !IsIpv4(endpoint.RemoteAddress)) return "本地地址或目标地址不是有效的 IPv4 地址";
        if (endpoint.LocalPort is < 0 or > 65535 || endpoint.RemotePort is < 1 or > 65535) return "端口号超出有效范围";
        return null;
    }
    private static bool IsIpv4(string value) => IPAddress.TryParse(value, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
}
