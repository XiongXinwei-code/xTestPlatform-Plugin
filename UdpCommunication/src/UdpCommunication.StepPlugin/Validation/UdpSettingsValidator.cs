using System.Net;
using UdpCommunication.StepPlugin.Transport;

namespace UdpCommunication.StepPlugin.Validation;

public static class UdpSettingsValidator
{
    public static string? ValidateEndpoint(UdpEndpointOptions endpoint)
    {
        if (!IPAddress.TryParse(endpoint.LocalAddress, out var localAddress)
            || !IPAddress.TryParse(endpoint.RemoteAddress, out var remoteAddress))
        {
            return "\u672c\u5730\u5730\u5740\u6216\u76ee\u6807\u5730\u5740\u4e0d\u662f\u6709\u6548\u7684 IP \u5730\u5740";
        }

        if (localAddress.AddressFamily != remoteAddress.AddressFamily)
        {
            return "\u672c\u5730\u5730\u5740\u4e0e\u76ee\u6807\u5730\u5740\u5fc5\u987b\u4f7f\u7528\u76f8\u540c\u7684 IP \u5730\u5740\u65cf";
        }

        if (endpoint.LocalPort is < 0 or > 65535 || endpoint.RemotePort is < 1 or > 65535) return "\u7aef\u53e3\u53f7\u8d85\u51fa\u6709\u6548\u8303\u56f4";
        return null;
    }
}
