namespace CAN.Flash.Executors;

/// <summary>CRC 计算工具</summary>
internal static class FlashCrc
{
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    /// <summary>标准 CRC-32/ISO-HDLC</summary>
    public static uint Crc32(IEnumerable<byte[]> blocks)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (var block in blocks)
            foreach (var b in block)
                crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>字节累加和（取低 32 位）</summary>
    public static uint Checksum(IEnumerable<byte[]> blocks)
    {
        uint sum = 0;
        foreach (var block in blocks)
            foreach (var b in block)
                sum += b;
        return sum;
    }
}
