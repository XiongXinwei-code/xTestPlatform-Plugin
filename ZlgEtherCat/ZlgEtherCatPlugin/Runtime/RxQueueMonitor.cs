using System.Diagnostics;

namespace ZlgEtherCat.Runtime;

public sealed record RxQueueStatus(
    bool Available, bool Monitoring, uint Capacity, uint CurrentDepth, uint PeakDepth,
    int HighWatermarkPercent, uint HighWatermarkDepth, int BacklogWarningMs, int AlarmRepeatMs, bool LatestOnly,
    bool HighWatermarkActive, bool BacklogWarningActive, bool OverflowSuspected,
    long HighWatermarkEvents, long BacklogWarningEvents, long FullQueueObservations,
    long? LastObservationTimestamp, double? ObservationAgeMs, double MaxPollGapMs,
    long? LastRecoveryTimestamp, string? LastAlarm, string? NotificationError,
    long? HardwareDroppedFrames, bool HardwareDroppedFramesAvailable);

public sealed record RxQueueNotice(string Message, RxQueueStatus Status);

internal sealed class RxQueueMonitor
{
    private readonly object _sync = new();
    private bool _queueMode, _monitoring, _latestOnly;
    private uint _capacity, _currentDepth, _peakDepth, _highWatermarkDepth;
    private int _highWatermarkPercent, _backlogWarningMs, _alarmRepeatMs;
    private bool _highWatermarkActive, _backlogWarningActive, _overflowSuspected;
    private long _highWatermarkEvents, _backlogWarningEvents, _fullQueueObservations;
    private long? _lastObservation, _backlogSince, _lastPoll, _lastRecovery, _lastNotice;
    private double _maxPollGapMs;
    private string? _lastAlarm, _notificationError;
    // One coalesced pending summary; counters and recovery time live independently of notification delivery.
    private bool _pendingNotice;

    internal void Reset(bool queueMode, uint capacity, int highWaterPercent, int backlogWarningMs, int alarmRepeatMs, bool latestOnly, long now)
    {
        if (highWaterPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(highWaterPercent));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backlogWarningMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alarmRepeatMs);
        lock (_sync)
        {
            _queueMode = queueMode;
            _monitoring = queueMode;
            _capacity = capacity;
            _highWatermarkPercent = highWaterPercent;
            _highWatermarkDepth = queueMode && capacity > 0 ? (uint)(((ulong)capacity * (uint)highWaterPercent + 99) / 100) : 0;
            _backlogWarningMs = backlogWarningMs;
            _alarmRepeatMs = alarmRepeatMs;
            _latestOnly = latestOnly;
            _currentDepth = _peakDepth = 0;
            _highWatermarkActive = _backlogWarningActive = _overflowSuspected = false;
            _highWatermarkEvents = _backlogWarningEvents = _fullQueueObservations = 0;
            _lastObservation = _backlogSince = _lastRecovery = _lastNotice = null;
            _lastPoll = now;
            _maxPollGapMs = 0;
            _lastAlarm = _notificationError = null;
            _pendingNotice = false;
        }
    }

    internal void BeginPoll(long now)
    {
        lock (_sync)
        {
            if (!_monitoring) return;
            if (_lastPoll is { } previous)
            {
                _maxPollGapMs = Math.Max(_maxPollGapMs, ElapsedMs(previous, now));
                now = Math.Max(previous, now);
            }
            _lastPoll = now;
        }
    }

    internal void ObserveDepth(uint depth, long now)
    {
        lock (_sync)
        {
            if (!_monitoring) return;
            if (_lastObservation is { } previous) now = Math.Max(previous, now);
            _lastObservation = now;
            _currentDepth = depth;
            _peakDepth = Math.Max(_peakDepth, depth);
            bool hadAlarm = _highWatermarkActive || _backlogWarningActive;
            bool high = _capacity > 0 && depth >= _highWatermarkDepth;
            if (high && !_highWatermarkActive) _highWatermarkEvents = Increment(_highWatermarkEvents);
            _highWatermarkActive = high;
            if (_capacity > 0 && depth >= _capacity)
            {
                _overflowSuspected = true;
                _fullQueueObservations = Increment(_fullQueueObservations);
            }

            if (depth == 0) _backlogSince = null;
            else _backlogSince ??= now;
            bool backlog = _backlogSince is { } first && ElapsedMs(first, now) >= _backlogWarningMs;
            if (backlog && !_backlogWarningActive) _backlogWarningEvents = Increment(_backlogWarningEvents);
            _backlogWarningActive = backlog;
            if (high || backlog)
            {
                _lastAlarm = AlarmMessage();
                _pendingNotice = true;
            }
            else if (hadAlarm)
            {
                _lastRecovery = now;
                _pendingNotice = true;
            }
        }
    }

    internal void Stop()
    {
        lock (_sync)
        {
            _monitoring = false;
            _pendingNotice = false;
            // Stopping observation is not evidence that the firmware queue recovered.
        }
    }

    internal RxQueueStatus Read(long now)
    {
        lock (_sync) return Snapshot(now);
    }

    internal RxQueueNotice? TryTakeNotice(long now)
    {
        lock (_sync)
        {
            if (!_monitoring || !(_pendingNotice || _highWatermarkActive || _backlogWarningActive)) return null;
            if (_lastNotice is { } previous && ElapsedMs(previous, now) < _alarmRepeatMs) return null;
            _lastNotice = now;
            _pendingNotice = false;
            string message = _highWatermarkActive || _backlogWarningActive
                ? AlarmMessage()
                : $"ZLG 输入队列高水位/持续积压告警已恢复；最近观测深度 {_currentDepth}。历史满队列疑似标记 {_overflowSuspected}，不代表已确认硬件丢帧。";
            return new RxQueueNotice(message, Snapshot(now));
        }
    }

    internal void RecordNotificationFailure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string description = error.ToString();
        if (description.Length > 2048) description = description[..2047] + "…";
        lock (_sync)
        {
            _notificationError = description;
            // Retry only the latest diagnostic summary, retaining the same minimum notification interval.
            if (_monitoring && _lastAlarm != null) _pendingNotice = true;
        }
    }

    private RxQueueStatus Snapshot(long now) => new(
        _queueMode && _capacity > 0, _monitoring, _capacity, _currentDepth, _peakDepth,
        _highWatermarkPercent, _highWatermarkDepth, _backlogWarningMs, _alarmRepeatMs, _latestOnly,
        _highWatermarkActive, _backlogWarningActive, _overflowSuspected,
        _highWatermarkEvents, _backlogWarningEvents, _fullQueueObservations,
        _lastObservation, _lastObservation is { } observed ? ElapsedMs(observed, now) : null,
        _maxPollGapMs, _lastRecovery, _lastAlarm, _notificationError, null, false);

    private string AlarmMessage() =>
        $"ZLG 输入队列告警：最近观测深度 {_currentDepth}，容量 {(_capacity > 0 ? _capacity.ToString() : "未知")}；" +
        $"高水位 {_highWatermarkActive}（阈值 {_highWatermarkDepth}）、持续积压 {_backlogWarningActive}（阈值 {_backlogWarningMs} ms）。" +
        "满队列仅为溢出疑似证据，SDK 未提供已确认的硬件丢帧数。";

    private static double ElapsedMs(long start, long now) => Math.Max(0, (double)now - start) * 1000 / Stopwatch.Frequency;
    private static long Increment(long value) => value == long.MaxValue ? value : value + 1;
}
