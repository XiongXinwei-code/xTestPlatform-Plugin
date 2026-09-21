namespace CAN.Adapters;

/// <summary>
/// 适配器接收诊断信息的公共记录器，供各 CAN 适配器实现 <see cref="ICanAdapterDiagnostics"/> 复用。
/// </summary>
internal sealed class CanReceiveDiagnostics(string adapterName)
{
    private readonly object _lock = new();
    private string _last = "尚未执行接收操作";

    /// <summary>返回最近一次读取的统计文本。</summary>
    public string Get()
    {
        lock (_lock) return _last;
    }

    /// <summary>开始一次读取，返回用于记录过程的会话对象。</summary>
    public CanReceiveSession BeginRead(uint? filterId, int timeoutMs, bool isFd)
        => new(this, adapterName, filterId, timeoutMs, isFd);

    internal void Set(string text)
    {
        lock (_lock) _last = text;
    }
}

/// <summary>单次读取过程的统计会话。</summary>
internal sealed class CanReceiveSession(
    CanReceiveDiagnostics owner, string adapterName, uint? filterId, int timeoutMs, bool isFd)
{
    private const int MaxRecordedIds = 8;

    private readonly List<uint> _seenIds = [];
    private int _receivedFrames;
    private int _filteredFrames;

    /// <summary>收到一帧但 ID 与目标不匹配。</summary>
    public void Filtered(uint id)
    {
        _receivedFrames++;
        _filteredFrames++;
        if (_seenIds.Count < MaxRecordedIds && !_seenIds.Contains(id))
            _seenIds.Add(id);
    }

    /// <summary>收到目标帧，读取成功。</summary>
    public void Matched()
    {
        _receivedFrames++;
        owner.Set(Build(true, ""));
    }

    /// <summary>读取超时或被取消。</summary>
    public void TimedOut(string extra = "")
    {
        owner.Set(Build(false, extra));
    }

    private string Build(bool success, string extra)
    {
        string target = filterId.HasValue ? $"0x{filterId.Value:X}" : "任意 ID";
        string ids = _seenIds.Count == 0 ? "无" : string.Join(", ", _seenIds.Select(id => $"0x{id:X}"));
        string text = $"{adapterName} 通道: 模式={(isFd ? "CAN FD" : "Classic")}, 目标 ID={target}, " +
                      $"等待时间={timeoutMs} ms, 收到帧={_receivedFrames}, ID 不匹配丢弃={_filteredFrames}, " +
                      $"出现的其他 ID={ids}, 结果={(success ? "成功" : "超时")}";
        return string.IsNullOrWhiteSpace(extra) ? text : $"{text}, {extra}";
    }
}
