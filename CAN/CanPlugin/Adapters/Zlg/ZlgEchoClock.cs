using System.Diagnostics;

namespace CAN.Adapters.Zlg;

/// <summary>发送前主机时刻 ≤ 发送回显采集时刻 ≤ 后续设备时间戳。
/// 仅使用因果下界，不把出队时刻当采集时刻，也不外推时钟频率。</summary>
internal sealed class ZlgEchoClock
{
    private readonly List<(ulong DeviceUs, long HostBefore, long HostAfter)> _anchors = [];
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");

    public void Reset() { _anchors.Clear(); Id = Guid.NewGuid().ToString("N"); }

    public void Add(ulong deviceUs, long before, long after)
    {
        if (deviceUs == 0 || after < before) { Reset(); return; }
        if (_anchors.Count > 0)
        {
            var last = _anchors[^1];
            // A counter discontinuity is diagnostic only; mapping never interpolates.
            var maxDeviceAdvance = (after - last.HostBefore) * 1_000_000d / Stopwatch.Frequency + 2000;
            if (deviceUs <= last.DeviceUs || deviceUs - last.DeviceUs > maxDeviceAdvance) { Reset(); return; }
        }
        _anchors.Add((deviceUs, before, after));
        if (_anchors.Count > 4096) _anchors.RemoveAt(0);
    }

    public bool IsCurrent(string id, long now) => id == Id && _anchors.Count > 0 &&
        now >= _anchors[^1].HostAfter && Stopwatch.GetElapsedTime(_anchors[^1].HostBefore, now).TotalMilliseconds <= 250;

    public long? LowerBound(ulong deviceUs, long now)
    {
        if (!IsCurrent(Id, now)) return null;
        for (var i = _anchors.Count - 1; i >= 0; i--)
        {
            var anchor = _anchors[i];
            if (deviceUs < anchor.DeviceUs) continue;
            var maxAdvance = (now - anchor.HostBefore) * 1_000_000d / Stopwatch.Frequency + 2000;
            return deviceUs - anchor.DeviceUs <= maxAdvance ? anchor.HostBefore : null;
        }
        return null;
    }
}
