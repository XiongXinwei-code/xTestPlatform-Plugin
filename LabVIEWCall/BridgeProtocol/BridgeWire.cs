using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabVIEWCallPlugin.BridgeProtocol;

/// <summary>Only trusted local child-process pipes carry this bounded, versioned-by-build protocol.</summary>
internal static class BridgeWire
{
    internal const int MaxMessageBytes = 128 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter<BridgeOperation>(null, allowIntegerValues: false) }
    };

    internal static T? Read<T>(Stream source) where T : class
    {
        Span<byte> prefix = stackalloc byte[sizeof(int)];
        int first = source.ReadByte();
        if (first < 0) return null;
        prefix[0] = (byte)first;
        source.ReadExactly(prefix[1..]);
        int size = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (size <= 0 || size > MaxMessageBytes)
            throw new InvalidDataException($"LabVIEW 桥消息长度无效或超出上限，字节数 {size}。");
        var payload = new byte[size];
        source.ReadExactly(payload);
        return JsonSerializer.Deserialize<T>(payload, Options)
            ?? throw new InvalidDataException("LabVIEW 桥消息不能为空。");
    }

    internal static void Write<T>(Stream destination, T message, Action? beforeWrite = null)
    {
        using var payload = new BoundedMessageStream();
        JsonSerializer.Serialize(payload, message, Options);
        Span<byte> prefix = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, checked((int)payload.Length));
        beforeWrite?.Invoke();
        destination.Write(prefix);
        payload.Position = 0;
        payload.CopyTo(destination);
        destination.Flush();
    }

    private sealed class BoundedMessageStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckSize(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CheckSize(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            CheckSize(1);
            base.WriteByte(value);
        }

        private void CheckSize(int count)
        {
            if (Position + count > MaxMessageBytes)
                throw new InvalidDataException("LabVIEW 桥消息超过 128 MiB，无法完整传输。");
        }
    }
}
