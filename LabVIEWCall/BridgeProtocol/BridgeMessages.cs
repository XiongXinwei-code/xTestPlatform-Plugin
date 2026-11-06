using System.Text.Json.Serialization;
using LabVIEWCallPlugin.LVadapter;

namespace LabVIEWCallPlugin.BridgeProtocol;

internal enum BridgeOperation
{
    Connect,
    Panel,
    Metadata,
    Edit,
    Load,
    Get,
    Set,
    Run
}

internal sealed class BridgeRequest
{
    [JsonRequired]
    public long Id { get; set; }

    [JsonRequired]
    public BridgeOperation Operation { get; set; }

    public int Port { get; set; } = 3363;
    public string Path { get; set; } = "";
    public string ControlIn { get; set; } = "";
    public string IndicatorIn { get; set; } = "";
    public string ParameterName { get; set; } = "";
    public uint Reference { get; set; }
    public short[] Type { get; set; } = [];
    public byte[] Data { get; set; } = [];
    public bool ShowPanel { get; set; }
    public bool ClosePanel { get; set; }

    internal ushort ValidatePort()
    {
        if (Port is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Port), Port, "VI Server 端口必须为 1 至 65535。");
        return (ushort)Port;
    }
}

internal sealed class BridgeResponse
{
    public long Id { get; set; }
    public string Kind { get; set; } = "Result";
    public string Phase { get; set; } = "";
    public string Error { get; set; } = "";
    public int ProcessId { get; set; }
    public uint Reference { get; set; }
    public short[] Type { get; set; } = [];
    public byte[] Data { get; set; } = [];
    public ConnectPanelInfo? Panel { get; set; }
}
