using System.Text.Json;
using System.Text.Json.Serialization;
using CAN.Runtime;

namespace CAN.CANopen.Cyclic;

/// <summary>单次求值后的 JSON 契约；不读文件、不执行表达式、不保留上下文。</summary>
public static class CyclicInputParser
{
    private static readonly JsonSerializerOptions Options = new()
    { MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public sealed record FrameInput(uint CobId, string DataHex);
    public sealed record PointInput(int OffsetMs, FrameInput[] Frames);
    public sealed record StopInput(byte NodeId, ushort Index, byte SubIndex, string DataHex);
    private static T[] Parse<T>(string json)
    {
        if (json.Length > 8_000_000) throw new ArgumentException("周期服务 JSON 超过 8 MB 限制");
        return JsonSerializer.Deserialize<T[]>(json, Options) ?? throw new ArgumentException("JSON 不能为 null");
    }
    public static CanTrajectory Trajectory(string json, int durationMs)
    {
        var points = Parse<PointInput>(json);
        if (points.Length is 0 or > 100000) throw new ArgumentException("点表数量必须为 1~100000");
        return new(points.Select(p => new TimedCanPoint(p.OffsetMs,
            (p.Frames ?? throw new ArgumentException("Frames 不能为空")).Select(f => new OrderedFrame(f.CobId,
                CanopenExecutorHelper.ParseHex(f.DataHex ?? throw new ArgumentException("DataHex 不能为空")))).ToArray())).ToArray(), durationMs);
    }
    public static ControlledStopOptions? Stop(string json, int timeoutMs)
    {
        var commands = Parse<StopInput>(json);
        if (commands.Length == 0) return null;
        if (commands.Length > 32) throw new ArgumentException("停机命令最多 32 条");
        return new(commands.Select(c => new StopSdoCommand(c.NodeId, c.Index, c.SubIndex,
            CanopenExecutorHelper.ParseHex(c.DataHex ?? throw new ArgumentException("DataHex 不能为空")))).ToArray(), timeoutMs);
    }
}
