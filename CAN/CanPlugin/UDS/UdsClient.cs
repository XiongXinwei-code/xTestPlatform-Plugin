using CAN.Adapters;
using CAN.Models;
using CAN.UDS.IsoTp;
using System.Runtime.CompilerServices;

namespace CAN.UDS;

/// <summary>UDS 响应结构</summary>
public class UdsResponse
{
    /// <summary>是否因等待 ECU 响应超时而生成。</summary>
    public bool IsTimeout { get; set; }

    /// <summary>适配器提供的接收诊断信息。</summary>
    public string DiagnosticMessage { get; set; } = "";

    /// <summary>请求的服务、收发标识和本轮等待时间，不包含请求负载。</summary>
    public string RequestDescription { get; set; } = "";

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

        var description = NegativeResponseCode switch
        {
            0x10 => "设备拒绝请求",
            0x11 => "不支持该服务",
            0x12 => "不支持该子功能",
            0x13 => "请求长度或格式错误",
            0x14 => "响应长度超出限制",
            0x21 => "设备忙，请稍后重试",
            0x22 => "执行条件不满足",
            0x24 => "请求顺序错误",
            0x25 => "子网组件未响应",
            0x26 => "设备故障阻止执行请求",
            0x31 => "请求参数超出范围",
            0x33 => "访问权限不足",
            0x35 => "密钥无效",
            0x36 => "尝试次数超出限制",
            0x37 => "规定的等待时间尚未结束",
            0x70 => "设备不接受上传或下载请求",
            0x71 => "数据传输已挂起",
            0x72 => "编程操作失败",
            0x73 => "传输块序号错误",
            0x78 => "请求已接收，等待设备完成处理",
            0x7E => "当前会话不支持该子功能",
            0x7F => "当前会话不支持该服务",
            _ => "未知否定响应码"
        };
        return $"NRC 0x{NegativeResponseCode:X2}：{description}";
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
    private readonly int _transactionTimeoutMs;
    private readonly ICanAdapter _adapter;
    private readonly IsoTpTransport _transport;
    private readonly int _responseTimeoutMs;
    private readonly int _p2StarTimeoutMs; // NRC 0x78 后的扩展超时
    private readonly uint _txId;
    private readonly uint _rxId;

    public UdsClient(ICanAdapter adapter, uint txId, uint rxId,
        int responseTimeoutMs = 5000, int p2StarTimeoutMs = 10000,
        CanFrameType frameType = CanFrameType.Standard, bool useFd = false,
        int maxResponseBytes = IsoTpTransport.DefaultMaxReceivePayloadBytes, int transactionTimeoutMs = 60000)
    {
        _adapter = adapter;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(transactionTimeoutMs);
        _transactionTimeoutMs = transactionTimeoutMs;
        if (responseTimeoutMs == 0 || responseTimeoutMs < -1) throw new ArgumentOutOfRangeException(nameof(responseTimeoutMs));
        _transport = new IsoTpTransport(adapter, txId, rxId, frameType, useFd, maxResponseBytes,
            responseTimeoutMs == -1 ? transactionTimeoutMs : responseTimeoutMs);
        _responseTimeoutMs = responseTimeoutMs == -1 ? int.MaxValue : responseTimeoutMs;
        _p2StarTimeoutMs = p2StarTimeoutMs;
        _txId = txId;
        _rxId = rxId;
    }

    /// <summary>发送 UDS 请求并等待响应</summary>
    public Task<UdsResponse> RequestAsync(byte[] requestData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestData);
        if (requestData.Length == 0) throw new ArgumentException("UDS 请求不能为空", nameof(requestData));
        var request = requestData.ToArray();
        return CAN.Runtime.CanProtocolTransaction.RunAsync(_adapter, _transactionTimeoutMs, async token =>
        {
            await _transport.SendAsync(request, token).ConfigureAwait(false);
            var response = await WaitForResponseAsync(request, token).ConfigureAwait(false);
            if (response.IsTimeout || response.ProtocolError != null)
                CAN.Runtime.CanProtocolTransaction.Invalidate(_adapter, response.GetNrcDescription());
            return response;
        }, ct);
    }

    /// <summary>发送 UDS 请求（不等待响应，用于功能寻址广播等）</summary>
    public async Task SendOnlyAsync(byte[] requestData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestData);
        if (requestData.Length == 0) throw new ArgumentException("UDS 请求不能为空", nameof(requestData));
        var request = requestData.ToArray();
        await CAN.Runtime.CanProtocolTransaction.RunAsync(_adapter, _transactionTimeoutMs, async token =>
        {
            await _transport.SendAsync(request, token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    private async Task<UdsResponse> WaitForResponseAsync(byte[] request, CancellationToken ct)
    {
        byte requestSid = request[0];
        int timeout = _responseTimeoutMs;

        while (!ct.IsCancellationRequested)
        {
            byte[]? raw;
            try { raw = await _transport.ReceiveAsync(timeout, ct).ConfigureAwait(false); }
            catch (CAN.Runtime.CanReceiveTimeoutException ex)
            {
                ct.ThrowIfCancellationRequested();
                CAN.Runtime.CanProtocolTransaction.Remaining(int.MaxValue);
                return CreateTimeoutResponse(requestSid, timeout, ex);
            }
            if (raw == null || raw.Length == 0)
            {
                ct.ThrowIfCancellationRequested();
                return CreateTimeoutResponse(requestSid, timeout);
            }

            var response = ParseResponse(raw);
            response.RequestDescription = DescribeRequest(requestSid, timeout);
            ValidateResponse(request, response);

            // 处理 NRC 0x78 - Response Pending
            if (!response.IsPositive && response.ProtocolError == null && response.NegativeResponseCode == 0x78)
            {
                timeout = _p2StarTimeoutMs;
                continue; // 继续等待
            }

            return response;
        }

        ct.ThrowIfCancellationRequested();
        return CreateTimeoutResponse(requestSid, timeout);
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
        if (response.ProtocolError != null) return;
        int echoOffset = requestSid == 0x31 ? 2 : 1;
        int echoLength = requestSid switch { 0x22 or 0x2E or 0x2F or 0x31 => 2, 0x36 => 1, _ => 0 };
        if (echoLength > 0 && (request.Length < echoOffset + echoLength || raw.Length < echoOffset + echoLength ||
            !request.AsSpan(echoOffset, echoLength).SequenceEqual(raw.AsSpan(echoOffset, echoLength))))
        {
            response.ProtocolError = $"SID 0x{requestSid:X2} 正响应标识/序号回显缺失或不匹配";
            return;
        }
        // 0x22 至少需要一个数据字节；0x10 必须携带 P2/P2*；下载协商长度不可截断。
        int minimum = requestSid switch { 0x22 => 4, 0x10 => 6, _ => 1 };
        if (raw.Length < minimum) response.ProtocolError = $"SID 0x{requestSid:X2} 正响应长度不足（至少 {minimum} 字节）";
        if (requestSid is 0x34 or 0x35)
        {
            int width = raw.Length > 1 ? raw[1] >> 4 : 0;
            if (width is < 1 or > 4 || (raw[1] & 0x0F) != 0 || raw.Length != width + 2)
                response.ProtocolError = "下载/上传正响应的块长度格式或数据长度非法";
        }
    }

    private string DescribeRequest(byte requestSid, int timeout)
        => $"SID=0x{requestSid:X2}，TX=0x{_txId:X}，RX=0x{_rxId:X}，本轮等待={timeout} ms";

    private UdsResponse CreateTimeoutResponse(byte requestSid, int timeout, Exception? receiveError = null)
    {
        string diagnostics = _adapter is ICanAdapterDiagnostics provider
            ? provider.GetReceiveDiagnostics()
            : "适配器未提供接收诊断";
        if (receiveError != null) diagnostics += $"；接收等待异常：{receiveError}";

        return new UdsResponse
        {
            IsTimeout = true,
            DiagnosticMessage = $"{DescribeRequest(requestSid, timeout)}；{diagnostics}",
            RequestDescription = DescribeRequest(requestSid, timeout),
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
