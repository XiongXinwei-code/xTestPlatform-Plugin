using System.Collections.Concurrent;
using System.Diagnostics;
using CAN.Adapters;
using CAN.Models;
using CiA402.Core;

namespace CiA402.Tests;

public sealed class FakeCanAdapter : ICanAdapter
{
    public ConcurrentQueue<CanMessage> Incoming { get; } = new();
    public ConcurrentQueue<CanMessage> Sent { get; } = new();
    public Dictionary<(ushort, byte), byte[]> Objects { get; } = new();
    public bool IsConnected { get; private set; } = true;
    public bool Respond { get; set; } = true;
    public void Open(CanAdapterConfig config) => IsConnected = true;
    public void Close() => IsConnected = false;
    public void Write(CanMessage message)
    {
        Sent.Enqueue(message);
        if (!Respond || message.Id is < 0x601 or > 0x67F || message.Data.Length != 8) return;
        var index = (ushort)(message.Data[1] | (message.Data[2] << 8));
        var sub = message.Data[3];
        var response = new byte[8];
        response[1] = message.Data[1]; response[2] = message.Data[2]; response[3] = sub;
        if (message.Data[0] == 0x40)
        {
            if (Objects.TryGetValue((index, sub), out var data))
            {
                response[0] = (byte)(0x43 | ((4 - data.Length) << 2));
                data.CopyTo(response, 4);
            }
            else { response[0] = 0x80; ObjectCodec.Encode(0x06020000, 4, false).CopyTo(response, 4); }
        }
        else if ((message.Data[0] & 0xE3) == 0x23)
        {
            var length = 4 - ((message.Data[0] >> 2) & 3);
            Objects[(index, sub)] = message.Data[4..(4 + length)];
            response[0] = 0x60;
        }
        else throw new InvalidOperationException("测试只支持快速 SDO");
        Incoming.Enqueue(new CanMessage { Id = message.Id - 0x80, Data = response });
    }
    public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            if (Incoming.TryDequeue(out var frame)) return frame;
            Thread.Sleep(1);
        }
        return null;
    }
    public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default)
    {
        var frame = Read(timeoutMs, ct);
        return frame?.Id == id ? frame : null;
    }
    public void Dispose() => Close();
    public void Object(ushort index, byte sub, long value, int bytes) => Objects[(index, sub)] = ObjectCodec.Encode(value, bytes, false);
    public void Pdo(int number, bool transmit, uint cobId, string layout)
    {
        var entries = PdoLayout.Parse(layout);
        ushort communication = (ushort)((transmit ? 0x1800 : 0x1400) + number - 1);
        ushort mapping = (ushort)((transmit ? 0x1A00 : 0x1600) + number - 1);
        Object(communication, 1, cobId, 4); Object(communication, 2, 1, 1); Object(mapping, 0, entries.Count, 1);
        for (var i = 0; i < entries.Count; i++) Object(mapping, (byte)(i + 1), entries[i].Descriptor, 4);
    }
}
