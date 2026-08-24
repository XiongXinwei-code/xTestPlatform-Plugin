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

        records.Sort((a, b) => a.Address.CompareTo(b.Address));

        var buffer = new List<byte>(records[0].Data);
        uint start = records[0].Address;

        for (int i = 1; i < records.Count; i++)
        {
            var (address, data) = records[i];
            uint currentEnd = start + (uint)buffer.Count;

            if (address == currentEnd)
            {
                buffer.AddRange(data);
            }
            else if (address < currentEnd)
            {
                // 记录重叠：后写入的数据覆盖先前内容
                int offset = (int)(address - start);
                int overlap = Math.Min(data.Length, buffer.Count - offset);
                for (int k = 0; k < overlap; k++)
                    buffer[offset + k] = data[k];
                if (data.Length > overlap)
                    buffer.AddRange(data[overlap..]);
            }
            else
            {
                // 存在空洞，收尾当前段并另起新段
                result.Add(new FlashSegment { StartAddress = start, Data = buffer.ToArray() });
                buffer.Clear();
                buffer.AddRange(data);
                start = address;
            }
        }

        result.Add(new FlashSegment { StartAddress = start, Data = buffer.ToArray() });
        return result;
    }
}
