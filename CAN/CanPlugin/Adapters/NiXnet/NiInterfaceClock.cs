namespace CAN.Adapters.NiXnet;

// Conservative, bracketed mapping: no assumed oscillator accuracy or interpolation.
// A frame between two device samples is no earlier than the first sample's host-before time.
internal sealed class NiInterfaceClock(long frequency)
{
    private readonly object _gate = new();
    private readonly List<(ulong Device, long Before, long After)> _samples = [];
    private string _clockId = Guid.NewGuid().ToString("N");
    public string ClockId { get { lock (_gate) return _clockId; } }
    public void Invalidate()
    { lock (_gate) { _samples.Clear(); _clockId = Guid.NewGuid().ToString("N"); } }
    public bool IsCurrent(string clockId, long now)
    {
        lock (_gate) return _samples.Count >= 2 && clockId == _clockId &&
            now >= _samples[^1].After && now - _samples[^1].After <= frequency / 4;
    }
    public void Add(ulong device, long before, long after)
    {
        lock (_gate)
        {
            if (device == 0 || after < before || after - before > frequency / 20) { Invalidate(); return; }
            if (_samples.Count > 0)
            {
                var old = _samples[^1];
                var elapsed = ((decimal)device - old.Device) * frequency / 10_000_000m;
                // Reject discontinuities beyond the sampling brackets and 2 ms diagnostic tolerance.
                if (device <= old.Device || before < old.After ||
                    elapsed < before - old.After - frequency / 500 || elapsed > after - old.Before + frequency / 500)
                    Invalidate();
            }
            _samples.Add((device, before, after));
            if (_samples.Count > 4096) _samples.RemoveAt(0);
        }
    }
    public long? LowerBound(ulong device, long now)
    {
        lock (_gate)
        {
            if (!IsCurrent(_clockId, now) || device < _samples[0].Device || device > _samples[^1].Device) return null;
            for (var i = _samples.Count - 1; i >= 0; i--)
                if (device >= _samples[i].Device) return _samples[i].Before;
            return null;
        }
    }
}
