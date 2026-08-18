namespace NiDaq.Helpers;

/// <summary>
/// NI-DAQmx 同步读写的软超时兜底工具。
/// DAQmx 的 Reader/Writer 均为阻塞式同步 API，不响应 CancellationToken；
/// 当 Stream.Timeout 设为 -1（无限等待）或驱动层超时未生效时，步骤会永久阻塞整个序列。
/// 因此统一在插件层用 <see cref="Task.WaitAsync(TimeSpan, CancellationToken)"/> 做软超时，超时抛 <see cref="TimeoutException"/>。
/// </summary>
public static class NiDaqTimeoutHelper
{
    /// <summary>默认软超时时间（毫秒），用于未配置超时的步骤</summary>
    public const int DefaultTimeoutMs = 10000;

    /// <summary>在后台线程执行同步 DAQmx 调用，并施加软超时</summary>
    public static async Task<T> RunWithTimeoutAsync<T>(Func<T> action, int timeoutMs, string operation, CancellationToken cancellationToken)
    {
        var task = System.Threading.Tasks.Task.Run(action, CancellationToken.None);
        try
        {
            return await task.WaitAsync(TimeSpan.FromMilliseconds(GetSoftTimeoutMs(timeoutMs)), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"NI-DAQmx {operation} 超时（{GetSoftTimeoutMs(timeoutMs)} ms），设备未在规定时间内响应");
        }
    }

    /// <summary>在后台线程执行同步 DAQmx 调用（无返回值），并施加软超时</summary>
    public static Task RunWithTimeoutAsync(Action action, int timeoutMs, string operation, CancellationToken cancellationToken) =>
        RunWithTimeoutAsync(() => { action(); return true; }, timeoutMs, operation, cancellationToken);

    /// <summary>软超时在驱动超时基础上留出余量，优先让 DAQmx 自身抛出更精确的超时异常</summary>
    private static int GetSoftTimeoutMs(int timeoutMs)
    {
        if (timeoutMs <= 0) return DefaultTimeoutMs;
        var soft = timeoutMs + 2000;
        return soft < timeoutMs ? int.MaxValue : soft;
    }
}
