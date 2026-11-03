using System.Diagnostics;

namespace CAN.Runtime;

internal readonly record struct CanCyclicControl(bool Enabled, int PeriodMs);

/// <summary>控制采样独立于可能排队的发送。只处理最新代次，不追补暂停期间的报文。</summary>
internal sealed class CanDynamicCyclicLoop
{
    private sealed record Snapshot(CanCyclicControl Control, long ChangedAt);
    private Snapshot _latest;
    internal CanCyclicControl CurrentControl => Volatile.Read(ref _latest).Control;
    private sealed class ControlChangedException : Exception;

    internal CanDynamicCyclicLoop(CanCyclicControl initial)
    {
        Validate(initial); _latest = new(initial, Stopwatch.GetTimestamp());
    }

    internal static void Validate(CanCyclicControl value)
    {
        if (value.PeriodMs <= 0) throw new ArgumentException("CAN 周期表达式结果必须是大于 0 的 int 毫秒数");
    }

    internal async Task RunAsync(int refreshMs, Func<CancellationToken, Task<CanCyclicControl>> sample,
        Func<CancellationToken, Action, Task> send, Action checkLifetime, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        async Task Observe(Func<Task> operation, string phase)
        {
            try { await operation().ConfigureAwait(false); }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException || !stop.IsCancellationRequested)
                    CanBackgroundJob.Current?.RecordFailure(new InvalidOperationException($"CAN 动态周期{phase}失败：{ex.Message}", ex));
                await stop.CancelAsync().ConfigureAwait(false); throw;
            }
        }
        await Task.WhenAll(Task.Run(() => Observe(SampleAsync, "控制采样")), Task.Run(() => Observe(SendAsync, "发送"))).ConfigureAwait(false);

        async Task SampleAsync()
        {
            while (true)
            {
                await Task.Delay(refreshMs, stop.Token).ConfigureAwait(false);
                checkLifetime(); stop.Token.ThrowIfCancellationRequested();
                var value = await sample(stop.Token).ConfigureAwait(false);
                checkLifetime(); stop.Token.ThrowIfCancellationRequested(); Validate(value);
                var old = Volatile.Read(ref _latest);
                if (old.Control != value) Volatile.Write(ref _latest, new(value, Stopwatch.GetTimestamp()));
            }
        }

        async Task SendAsync()
        {
            var expected = Volatile.Read(ref _latest);
            long due = AddPeriod(expected.ChangedAt, expected.Control.PeriodMs);
            while (true)
            {
                checkLifetime(); stop.Token.ThrowIfCancellationRequested();
                var current = Volatile.Read(ref _latest);
                if (!ReferenceEquals(current, expected))
                { expected = current; due = AddPeriod(current.ChangedAt, current.Control.PeriodMs); }
                double remaining = (due - Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency;
                if (!current.Control.Enabled || remaining > 0)
                {
                    await Task.Delay(!current.Control.Enabled ? refreshMs : Math.Max(1, Math.Min(refreshMs, (int)Math.Ceiling(remaining))), stop.Token).ConfigureAwait(false);
                    continue;
                }
                void CheckAdmission()
                {
                    checkLifetime(); stop.Token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(Volatile.Read(ref _latest), current)) throw new ControlChangedException();
                }
                long started = Stopwatch.GetTimestamp();
                try { CheckAdmission(); await send(stop.Token, CheckAdmission).ConfigureAwait(false); }
                catch (ControlChangedException) { continue; } // This frame was never admitted; retain the counter.
                long now = Stopwatch.GetTimestamp();
                long next = AddPeriod(started, current.Control.PeriodMs);
                due = next > now ? next : AddPeriod(now, current.Control.PeriodMs);
            }
        }
    }

    private static long AddPeriod(long start, int periodMs) => start + (long)(periodMs * (double)Stopwatch.Frequency / 1000d);
}
