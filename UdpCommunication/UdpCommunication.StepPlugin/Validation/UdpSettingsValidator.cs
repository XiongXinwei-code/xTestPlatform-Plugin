using System.Net;
using UdpCommunication.Transport;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.Validation;

public static class UdpSettingsValidator
{
    public static string? ValidateEndpoint(UdpEndpointOptions endpoint)
    {
        if (!IPAddress.TryParse(endpoint.LocalAddress, out var localAddress)
            || !IPAddress.TryParse(endpoint.RemoteAddress, out var remoteAddress))
        {
            return "本地地址或目标地址不是有效的 IP 地址";
        }

        if (localAddress.AddressFamily != remoteAddress.AddressFamily)
        {
            return "本地地址与目标地址必须使用相同的 IP 协议族（IPv4/IPv6）";
        }

        if (endpoint.LocalPort is < 1 or > 65535 || endpoint.RemotePort is < 1 or > 65535)
        {
            return "端口号超出有效范围（1~65535；本地端口不允许 0，否则后续步骤无法引用）";
        }

        return null;
    }

    public static string? ValidateEndpointWithoutRemote(UdpEndpointOptions endpoint)
    {
        if (!IPAddress.TryParse(endpoint.LocalAddress, out _))
        {
            return "本地地址不是有效的 IP 地址";
        }

        if (endpoint.LocalPort is < 1 or > 65535)
        {
            return "本地端口超出有效范围（1~65535；不允许 0，否则后续步骤无法引用）";
        }

        return null;
    }

    public static string? ValidateLocalEndpoint(string localAddress, int localPort)
    {
        if (!IPAddress.TryParse(localAddress, out _))
        {
            return "本地地址不是有效的 IP 地址";
        }

        if (localPort is < 1 or > 65535)
        {
            return "本地端口超出有效范围（1~65535；不允许 0，否则后续步骤无法引用）";
        }

        return null;
    }

    public static string? ValidateRemoteEndpoint(string remoteAddress, int remotePort)
    {
        if (!IPAddress.TryParse(remoteAddress, out _))
        {
            return "目标地址不是有效的 IP 地址";
        }

        if (remotePort is < 1 or > 65535)
        {
            return "目标端口超出有效范围（1~65535）";
        }

        return null;
    }
}
