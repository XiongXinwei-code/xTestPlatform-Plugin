namespace CAN.Flash.HexParser;

/// <summary>
/// 数据段构建工具：将解析出的零散记录按地址排序并合并为连续段。
/// </summary>
internal static class SegmentBuilder
{
    /// <summary>
    /// 将零散记录合并为地址连续的数据段。相邻或重叠的记录会被合并，
    /// 中间存在空洞的记录保持为独立段。
    /// </summary>
    public static IReadOnlyList<FlashSegment> Merge(List<(uint Address, byte[] Data)> records)
    {
        var result = new List<FlashSegment>();
        if (records.Count == 0)
            return result;

        foreach (var record in records)
            if ((ulong)record.Address + (uint)record.Data.Length > 0x1_0000_0000UL)
                throw new InvalidDataException($"固件记录 0x{record.Address:X8} 长度 {record.Data.Length} 越过32位地址空间");

        // 先只按地址计算连续区间，再按原文件记录顺序拷贝数据。
        // 不能对调用者记录原地排序，否则地址更低的后写记录会被旧记录反向覆盖。
        var sorted = records.Where(r => r.Data.Length > 0).OrderBy(r => r.Address).ToArray();
        if (sorted.Length == 0) return result;
        uint start = sorted[0].Address;
        ulong end = (ulong)start + (uint)sorted[0].Data.Length;
        foreach (var record in sorted.Skip(1))
        {
            if (record.Address > end)
            {
                result.Add(CreateSegment(start, end));
                start = record.Address;
                end = record.Address;
            }
            end = Math.Max(end, (ulong)record.Address + (uint)record.Data.Length);
        }
        result.Add(CreateSegment(start, end));

        foreach (var record in records)
        {
            if (record.Data.Length == 0) continue;
            int low = 0, high = result.Count - 1;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (result[middle].StartAddress <= record.Address) low = middle;
                else high = middle - 1;
            }
            var segment = result[low];
            record.Data.CopyTo(segment.Data, checked((int)(record.Address - segment.StartAddress)));
        }
        return result;
    }

    private static FlashSegment CreateSegment(uint start, ulong end)
    {
        if (end - start > int.MaxValue)
            throw new InvalidDataException($"固件连续区间 0x{start:X8} 的长度超过支持的缓冲区上限");
        return new FlashSegment { StartAddress = start, Data = new byte[checked((int)(end - start))] };
    }
}
