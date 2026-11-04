using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ethernet.Runtime;

/// <summary>按 socket 实例保存分帧尾部；连接重开不继承旧缓冲，同一连接读取必须串行。</summary>
internal sealed class TcpReceiveBuffer
{
    private static readonly ConditionalWeakTable<TcpClient, TcpReceiveBuffer> Buffers = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<byte> _pending = new();
    internal static TcpReceiveBuffer For(TcpClient client) => Buffers.GetValue(client, _ => new());

    internal async Task<byte[]> ReadAsync(TcpClient client, int expectedLength, byte[]? delimiter,
        bool includeDelimiter, int maxBytes, CancellationToken token, Action check)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var stream = TcpConnectionManager.GetStream(client);
            int searchFrom = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested(); check();
                int consumed = 0, returned = 0;
                if (delimiter != null)
                {
                    int offset = FindDelimiter(searchFrom, delimiter);
                    if (offset >= 0)
                    {
                        consumed = offset + delimiter.Length;
                        if (consumed > maxBytes) throw TooLong(maxBytes);
                        returned = includeDelimiter ? consumed : offset;
                    }
                    searchFrom = Math.Max(0, _pending.Count - delimiter.Length + 1);
                }
                else if (_pending.Count > 0 && (expectedLength == 0 || _pending.Count >= expectedLength))
                    returned = consumed = expectedLength > 0 ? expectedLength : Math.Min(_pending.Count, maxBytes);
                if (consumed > 0)
                {
                    var data = _pending.GetRange(0, returned).ToArray();
                    _pending.RemoveRange(0, consumed);
                    return data;
                }
                if (_pending.Count >= maxBytes) throw TooLong(maxBytes);
                var buffer = new byte[Math.Min(65535, maxBytes - _pending.Count)];
                int count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException($"TCP 对端已关闭，缓冲中仅有 {_pending.Count} 字节，尚未收到完整响应");
                // 保留已从网络消费的内容；取消/超时不发布结果，也不丢弃下一帧尾部。
                _pending.AddRange(buffer.AsSpan(0, count).ToArray());
            }
        }
        finally { _gate.Release(); }
    }
    private int FindDelimiter(int start, byte[] delimiter)
    {
        int index = CollectionsMarshal.AsSpan(_pending).Slice(start).IndexOf(delimiter);
        return index < 0 ? -1 : start + index;
    }
    private static InvalidDataException TooLong(int max) => new($"TCP 响应超过 MaxBytes={max}，未消费缓冲结果；请检查分帧配置或关闭连接清空旧数据");
}
