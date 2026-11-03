using System.IO;
using System.Text;
using System.Text.Json;

namespace CAN.Dbc;

internal static class DbcJson
{
    internal const int MaximumBytes = 16 * 1024 * 1024;
    internal static async Task<string> SerializeAsync(object value, CancellationToken ct, int maximumBytes = MaximumBytes)
    {
        using var output = new LimitedStream(maximumBytes, ct);
        await JsonSerializer.SerializeAsync(output, value, DbcExecution.JsonOptions, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }

    private sealed class LimitedStream(int maximum, CancellationToken token) : MemoryStream
    {
        private void Check(int count)
        {
            token.ThrowIfCancellationRequested();
            if (count > maximum - Length) throw new InvalidOperationException($"DBC JSON 结果超过 {maximum} 字节上限，请缩短时窗或减少报文，未发布部分结果");
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
    }
}
