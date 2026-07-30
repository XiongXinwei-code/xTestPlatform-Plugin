using CAN.Adapters;
using CAN.Models;
using CAN.UDS.IsoTp;

namespace CAN.UDS;

/// <summary>UDS 响应结构</summary>
public class UdsResponse
{
    /// <summary>服务 ID（正响应 = 请求 SID + 0x40）</summary>
    public byte ServiceId { get; set; }

    /// <summary>子功能或 NRC（Negative Response Code）</summary>
    public byte SubFunction { get; set; }

    /// <summary>响应数据（不含 SID）</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>原始响应字节</summary>
    public byte[] RawBytes { get; set; } = [];

    /// <summary>是否为正响应</summary>
    public bool IsPositive => ServiceId != 0x7F;

    /// <summary>否定响应码（仅 IsPositive=false 时有效）</summary>
    public byte NegativeResponseCode => IsPositive ? (byte)0 : (Data.Length > 0 ? Data[0] : (byte)0);

    /// <summary>获取 NRC 描述</summary>
    public string GetNrcDescription() => NegativeResponseCode switch
    {
        0x10 => "General Reject",
        0x11 => "Service Not Supported",
        0x12 => "Sub-Function Not Supported",
        0x13 => "Incorrect Message Length Or Invalid Format",
        0x14 => "Response Too Long",
        0x21 => "Busy Repeat Request",
        0x22 => "Conditions Not Correct",
        0x24 => "Request Sequence Error",
        0x25 => "No Response From Sub-Net Component",
        0x26 => "Failure Prevents Execution Of Requested Action",
        0x31 => "Request Out Of Range",
        0x33 => "Security Access Denied",
        0x35 => "Invalid Key",
        0x36 => "Exceeded Number Of Attempts",
        0x37 => "Required Time Delay Not Expired",
        0x70 => "Upload/Download Not Accepted",
        0x71 => "Transfer Data Suspended",
        0x72 => "General Programming Failure",
        0x73 => "Wrong Block Sequence Counter",
        0x78 => "Request Correctly Received - Response Pending",
        0x7E => "Sub-Function Not Supported In Active Session",
        0x7F => "Service Not Supported In Active Session",
        _ => $"Unknown NRC (0x{NegativeResponseCode:X2})"
    };
}

/// <summary>
/// UDS 客户端，封装请求/响应逻辑，处理 NRC 0x78 (ResponsePending)。
/// </summary>
public sealed class UdsClient
{
    private readonly IsoTpTransport _transport;
    private readonly int _responseTimeoutMs;
    private readonly int _p2StarTimeoutMs; // NRC 0x78 后的扩展超时

    public UdsClient(ICanAdapter adapter, uint txId, uint rxId,
        int responseTimeoutMs = 5000, int p2StarTimeoutMs = 10000,
        CanFrameType frameType = CanFrameType.Standard, bool useFd = false)
    {
        _transport = new IsoTpTransport(adapter, txId, rxId, frameType, useFd);
        _responseTimeoutMs = responseTimeoutMs;
        _p2StarTimeoutMs = p2StarTimeoutMs;
    }

    /// <summary>发送 UDS 请求并等待响应</summary>
    public async Task<UdsResponse> RequestAsync(byte[] requestData, CancellationToken ct = default)
    {
        await _transport.SendAsync(requestData, ct);
        return await WaitForResponseAsync(requestData[0], ct);
    }

    /// <summary>发送 UDS 请求（不等待响应，用于功能寻址广播等）</summary>
    public async Task SendOnlyAsync(byte[] requestData, CancellationToken ct = default)
    {
        await _transport.SendAsync(requestData, ct);
    }

    private async Task<UdsResponse> WaitForResponseAsync(byte requestSid, CancellationToken ct)
    {
        int timeout = _responseTimeoutMs;

        while (!ct.IsCancellationRequested)
        {
            var raw = await _transport.ReceiveAsync(timeout, ct);
            if (raw == null || raw.Length == 0)
            {
                return new UdsResponse
                {
                    ServiceId = 0x7F,
                    Data = [(byte)requestSid, 0x10], // General Reject as timeout
                    RawBytes = []
                };
            }

            var response = ParseResponse(raw);

            // 处理 NRC 0x78 - Response Pending
            if (!response.IsPositive && response.NegativeResponseCode == 0x78)
            {
                timeout = _p2StarTimeoutMs;
                continue; // 继续等待
            }

            return response;
        }

        return new UdsResponse
        {
            ServiceId = 0x7F,
            Data = [(byte)requestSid, 0x10],
            RawBytes = []
        };
    }

    private static UdsResponse ParseResponse(byte[] raw)
    {
        if (raw.Length == 0) return new UdsResponse { ServiceId = 0x7F, RawBytes = raw };

        var response = new UdsResponse
        {
            ServiceId = raw[0],
            RawBytes = raw
        };

        if (raw[0] == 0x7F && raw.Length >= 3)
        {
            // 否定响应: 7F [SID] [NRC]
            response.SubFunction = raw[1];
            response.Data = raw.Length > 2 ? raw[2..] : [];
        }
        else
        {
            // 正响应: [SID+0x40] [SubFunc/Data...]
            response.Data = raw.Length > 1 ? raw[1..] : [];
            if (raw.Length > 1) response.SubFunction = raw[1];
        }

        return response;
    }
}
