using System.Net;

namespace CAN.Adapters.Zlg;

/// <summary>Resolved ZLG connection inputs. Credentials are deliberately excluded from diagnostics.</summary>
public sealed class ZlgConnectionOptions
{
    public string RemoteAddress { get; init; } = "";
    public int RemotePort { get; init; } = 4001;
    public int LocalPort { get; init; } = 4001;
    public int NetworkWorkMode { get; init; }
    public string ConfigurationPath { get; init; } = "";
    public string CloudUserName { get; init; } = "";
    public string CloudPassword { get; init; } = "";
    public string CloudHttpServer { get; init; } = "";
    public int CloudHttpPort { get; init; } = 80;
    public string CloudAuthServer { get; init; } = "";
    public int CloudAuthPort { get; init; } = 80;

    public void Validate(uint type, bool checkFile = true)
    {
        if (ZlgDeviceCatalog.IsNetwork(type))
        {
            if (NetworkWorkMode is not (0 or 1)) throw new ArgumentException("ZLG PC 网络角色必须为 Client (0) 或 Server (1)");
            if ((ZlgDeviceCatalog.IsUdp(type) || NetworkWorkMode == 0) &&
                (!IPAddress.TryParse(RemoteAddress, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                throw new ArgumentException("ZLG RemoteAddress 必须是有效的设备 IPv4 地址");
            ValidatePort(RemotePort, nameof(RemotePort));
            ValidatePort(LocalPort, nameof(LocalPort));
        }
        if (ZlgDeviceCatalog.RequiresFilePath(type))
        {
            if (string.IsNullOrWhiteSpace(ConfigurationPath) || ConfigurationPath.Contains('\0') || ConfigurationPath.Length > 259)
                throw new ArgumentException("ZLG ConfigurationPath 必须是有效的厂商配置文件路径，长度不能超过 259 字符且不能含 NUL");
            if (checkFile && !File.Exists(ConfigurationPath)) throw new ArgumentException("ZLG 厂商配置文件不存在，请检查 ConfigurationPath");
        }
        if (type == 46)
        {
            ValidateText(CloudUserName, "CloudUserName", 63, true);
            ValidateText(CloudPassword, "CloudPassword", 1024, true);
            ValidateText(CloudHttpServer, "CloudHttpServer", 253, true);
            ValidateText(CloudAuthServer, "CloudAuthServer", 253, true);
            if (string.IsNullOrWhiteSpace(CloudHttpServer) != string.IsNullOrWhiteSpace(CloudAuthServer))
                throw new ArgumentException("ZLG Cloud 两个服务器地址必须明确填写，不能继承进程内残留的服务器配置");
            if (CloudHttpServer.Length > 0 && (Uri.CheckHostName(CloudHttpServer) == UriHostNameType.Unknown || Uri.CheckHostName(CloudAuthServer) == UriHostNameType.Unknown))
                throw new ArgumentException("ZLG Cloud 服务器必须填写主机名或 IP，不包含 URL 路径");
            ValidatePort(CloudHttpPort, nameof(CloudHttpPort));
            ValidatePort(CloudAuthPort, nameof(CloudAuthPort));
        }
    }

    public void ValidateScalarValues(uint type)
    {
        if (ZlgDeviceCatalog.IsNetwork(type))
        {
            if (NetworkWorkMode is not (0 or 1)) throw new ArgumentException("ZLG PC 网络角色必须为 Client (0) 或 Server (1)");
            ValidatePort(RemotePort, nameof(RemotePort)); ValidatePort(LocalPort, nameof(LocalPort));
        }
        if (type == 46) { ValidatePort(CloudHttpPort, nameof(CloudHttpPort)); ValidatePort(CloudAuthPort, nameof(CloudAuthPort)); }
    }

    private static void ValidatePort(int port, string field)
    {
        if (port is < 1 or > 65535) throw new ArgumentException($"ZLG {field} 必须在 1~65535 之间");
    }
    private static void ValidateText(string value, string field, int maximum, bool required)
    {
        if (value.Contains('\0') || value.Length > maximum || required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"ZLG {field} 不能为空或超出 SDK 长度限制，且不能含 NUL");
    }
}
