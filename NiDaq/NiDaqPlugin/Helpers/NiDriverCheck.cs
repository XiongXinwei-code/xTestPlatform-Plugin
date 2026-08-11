namespace NiDaq.Helpers;

/// <summary>
/// NI DAQmx driver availability check.
/// </summary>
internal static class NiDriverCheck
{
    private static bool? _available;

    /// <summary>
    /// Returns true if NI DAQmx .NET API is loadable at runtime.
    /// </summary>
    public static bool IsDriverAvailable
    {
        get
        {
            if (_available.HasValue) return _available.Value;
            try
            {
                // Actually touch the native NI-DAQmx driver (P/Invoke), not just the managed assembly
                _ = NationalInstruments.DAQmx.DaqSystem.Local.Devices;
                _available = true;
            }
            catch
            {
                _available = false;
            }
            return _available.Value;
        }
    }

    /// <summary>
    /// Throws if NI driver is not available. Call at start of each executor.
    /// </summary>
    public static void EnsureDriver()
    {
        if (!IsDriverAvailable)
            throw new InvalidOperationException("未安装 NI-DAQmx 驱动，无法执行此步骤。请在目标机器上安装 NI-DAQmx 驱动。");
    }
}
