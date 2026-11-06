namespace LabVIEWCallPlugin.LVadapter;

/// <summary>读取已结束 VI 的稳定输出。旧桥只复制前缀，不返回长度。</summary>
internal static class LvOutputReader
{
    internal const int MaxTypeWords = 32767;
    internal const int MaxDataBytes = 8 * 1024 * 1024;

    internal static (IntPtr Reference, short[] Type, byte[] Data) Read(
        Func<short[], byte[], IntPtr> read, int typeWords, int dataBytes, CancellationToken ct,
        IntPtr expectedReference, Action? checkAdmission = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(typeWords);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(typeWords, MaxTypeWords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dataBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dataBytes, MaxDataBytes);
        void ReadCurrent(short[] words, byte[] data)
        {
            ct.ThrowIfCancellationRequested();
            checkAdmission?.Invoke();
            CheckTerminalReference(expectedReference, read(words, data));
        }
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            // 多留一个元素区分“恰好装满”和“返回前缀被截断”。
            var firstType = new short[typeWords + 1];
            var firstData = new byte[dataBytes + 1];
            ReadCurrent(firstType, firstData);
            ct.ThrowIfCancellationRequested();
            var secondType = new short[firstType.Length];
            var secondData = new byte[firstData.Length];
            Array.Fill(secondType, (short)-1);
            Array.Fill(secondData, byte.MaxValue);
            ReadCurrent(secondType, secondData);
            ct.ThrowIfCancellationRequested();

            int words = StablePrefixLength(firstType, secondType, (short)-1);
            int bytes = StablePrefixLength(firstData, secondData, byte.MaxValue);
            if (words < 2 || bytes == 0)
                throw new InvalidDataException("VI 输出为空或缺少类型描述。");
            if (words <= typeWords && bytes <= dataBytes)
                return (expectedReference, firstType[..words], firstData[..bytes]);
            if (words > typeWords)
                typeWords = Grow(typeWords, MaxTypeWords, "类型描述字数");
            if (bytes > dataBytes)
                dataBytes = Grow(dataBytes, MaxDataBytes, "数据字节数");
        }
    }

    // 端子访问沿用同一 VI 引用。桥未导出判断不同引用是否同一目标的接口。
    internal static void CheckTerminalReference(IntPtr expected, IntPtr returned)
    {
        if (returned == IntPtr.Zero || returned != expected)
            throw new InvalidDataException("端子访问返回空引用或更换了 VI 引用，已中止后续操作。");
    }

    private static int StablePrefixLength<T>(T[] first, T[] second, T secondFill)
        where T : unmanaged, IEquatable<T>
    {
        // 原生函数连续复制：对两次相同的输出，最后一个有效元素必改变至少一种底色。
        // 单一底色会把以 00 或 FF 结尾的合法数据误判为未写入。
        int length = Math.Max(first.AsSpan().LastIndexOfAnyExcept(default(T)),
            second.AsSpan().LastIndexOfAnyExcept(secondFill)) + 1;
        if (!first.AsSpan(0, length).SequenceEqual(second.AsSpan(0, length)))
            throw new InvalidDataException("两次读取的 VI 输出不一致，无法确定完整结果。请在 VI 返回前锁存指示器值。");
        return length;
    }

    private static int Grow(int current, int maximum, string unit)
    {
        if (current >= maximum)
            throw new InvalidDataException($"VI 输出超过读取上限（{unit} {maximum}）。");
        return Math.Min(current * 2, maximum);
    }
}
