using CAN.Adapters;
using CAN.Models;
using CAN.UDS.IsoTp;

namespace CAN.UDS;

/// <summary>UDS 响应结构</summary>
public class UdsResponse
{
    /// <summary>是否因等待 ECU 响应超时而生成。</summary>
    public bool IsTimeout { get; set; }

    /// <summary>适配器提供的接收诊断信息。</summary>
    public string DiagnosticMessage { get; set; } = "";

    /// <summary>服务 ID（正响应 = 请求 SID + 0x40）</summary>
    public byte ServiceId { get; set; }

    /// <summary>子功能或 NRC（Negative Response Code）</summary>
    public byte SubFunction { get; set; }

    /// <summary>响应数据（不含 SID）</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>原始响应字节</summary>
    public byte[] RawBytes { get; set; } = [];

    /// <summary>响应与请求不匹配时的协议错误描述（为 null 表示响应格式合法）</summary>
    public string? ProtocolError { get; set; }

    /// <summary>是否为正响应（SID 与子功能回显已校验）</summary>
    public bool IsPositive => !IsTimeout && ProtocolError == null && ServiceId != 0x7F;

    /// <summary>否定响应码（仅 IsPositive=false 时有效）</summary>
    public byte NegativeResponseCode => IsPositive || ProtocolError != null ? (byte)0 : (Data.Length > 0 ? Data[0] : (byte)0);

    /// <summary>获取 NRC 描述</summary>
    public string GetNrcDescription()
    {
        if (IsTimeout)
            return string.IsNullOrWhiteSpace(DiagnosticMessage)
                ? "等待 ECU 响应超时"
                : $"等待 ECU 响应超时；{DiagnosticMessage}";

        if (ProtocolError != null)
            return ProtocolError;

        return NegativeResponseCode switch
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

    /// <summary>用于步骤结果 Value，避免把本地超时误报为 ECU 返回 NRC 0x10。</summary>
    public string GetFailureValue() => IsTimeout
        ? $"Timeout; {DiagnosticMessage}"
        : ProtocolError != null
            ? $"InvalidResponse={BitConverter.ToString(RawBytes).Replace("-", " ")}"
            : $"NRC=0x{NegativeResponseCode:X2}";
}

/// <summary>
/// UDS 客户端，封装请求/响应逻辑，处理 NRC 0x78 (ResponsePending)。
/// </summary>
public sealed class UdsClient
{
    private readonly ICanAdapter _adapter;
    private readonly IsoTpTransport _transport;
    private readonly int _responseTimeoutMs;
    private readonly int _p2StarTimeoutMs; // NRC 0x78 后的扩展超时

    public UdsClient(ICanAdapter adapter, uint txId, uint rxId,
        int responseTimeoutMs = 5000, int p2StarTimeoutMs = 10000,
        CanFrameType frameType = CanFrameType.Standard, bool useFd = false)
    {
        _adapter = adapter;
        _transport = new IsoTpTransport(adapter, txId, rxId, frameType, useFd);
        _responseTimeoutMs = responseTimeoutMs;
        _p2StarTimeoutMs = p2StarTimeoutMs;
    }

    /// <summary>发送 UDS 请求并等待响应</summary>
    public async Task<UdsResponse> RequestAsync(byte[] requestData, CancellationToken ct = default)
    {
        await _transport.SendAsync(requestData, ct);
        return await WaitForResponseAsync(requestData, ct);
    }

    /// <summary>发送 UDS 请求（不等待响应，用于功能寻址广播等）</summary>
    public async Task SendOnlyAsync(byte[] requestData, CancellationToken ct = default)
    {
        await _transport.SendAsync(requestData, ct);
    }

    private async Task<UdsResponse> WaitForResponseAsync(byte[] request, CancellationToken ct)
    {
        byte requestSid = request[0];
        int timeout = _responseTimeoutMs;

        while (!ct.IsCancellationRequested)
        {
            var raw = await _transport.ReceiveAsync(timeout, ct);
            if (raw == null || raw.Length == 0)
            {
                return CreateTimeoutResponse(requestSid);
            }

            var response = ParseResponse(raw);
            ValidateResponse(request, response);

            // 处理 NRC 0x78 - Response Pending
            if (!response.IsPositive && response.ProtocolError == null && response.NegativeResponseCode == 0x78)
            {
                timeout = _p2StarTimeoutMs;
                continue; // 继续等待
            }

            return response;
        }

        return CreateTimeoutResponse(requestSid);
    }

    /// <summary>带子功能字节且正响应会回显子功能的服务</summary>
    private static readonly HashSet<byte> SubFunctionServices = [0x10, 0x11, 0x19, 0x27, 0x28, 0x31, 0x3E, 0x85, 0x87];

    /// <summary>校验响应 SID 与请求对应，并校验子功能回显</summary>
    private static void ValidateResponse(byte[] request, UdsResponse response)
    {
        var raw = response.RawBytes;
        byte requestSid = request[0];

        if (raw[0] == 0x7F)
        {
            if (raw.Length < 3)
                response.ProtocolError = $"否定响应长度不足: {BitConverter.ToString(raw)}";
            else if (raw[1] != requestSid)
                response.ProtocolError = $"否定响应服务 ID 不匹配: 期望 0x{requestSid:X2}，实际 0x{raw[1]:X2}";
            return;
        }

        byte expectedSid = (byte)(requestSid + 0x40);
        if (raw[0] != expectedSid)
        {
            response.ProtocolError = $"正响应服务 ID 不匹配: 期望 0x{expectedSid:X2}，实际 0x{raw[0]:X2}";
            return;
        }

        if (SubFunctionServices.Contains(requestSid) && request.Length >= 2)
        {
            byte expectedSub = (byte)(request[1] & 0x7F);
            if (raw.Length < 2 || (raw[1] & 0x7F) != expectedSub)
            {
                response.ProtocolError = raw.Length < 2
                    ? $"正响应缺少子功能回显: 期望 0x{expectedSub:X2}"
                    : $"正响应子功能不匹配: 期望 0x{expectedSub:X2}，实际 0x{raw[1] & 0x7F:X2}";
            }
        }
    }

    private UdsResponse CreateTimeoutResponse(byte requestSid)
    {
        string diagnostics = _adapter is ICanAdapterDiagnostics provider
            ? provider.GetReceiveDiagnostics()
            : $"适配器未提供接收诊断，等待时间={_responseTimeoutMs} ms";

        return new UdsResponse
        {
            IsTimeout = true,
            DiagnosticMessage = diagnostics,
            ServiceId = 0x7F,
            SubFunction = requestSid,
            Data = [],
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
