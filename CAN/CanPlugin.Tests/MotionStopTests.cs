using System.Diagnostics;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public class MotionStopTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CompletedMotionStopUsesCapturedResultDespiteLaterMonitoringOrNewMotion(int scenario)
    {
        using var resumeWaiter = new ManualResetEventSlim();
        using var waiterPaused = new ManualResetEventSlim();
        using var monitoringEntered = new ManualResetEventSlim();
        using var confirmMonitoring = new ManualResetEventSlim();
        var fake = new DelayedAdapter();
        var connection = new CanConnectionResource(fake);
        var service = connection.StartService("motion", [new("status", 0x181, 1, 100)]);
        var stopAcknowledged = false;
        fake.OnWrite = m =>
        {
            if (m.Id == 0x601)
            {
                stopAcknowledged = true;
                fake.Incoming.Add(new() { Id = 0x581, Data = [0x60, m.Data[1], m.Data[2], m.Data[3], 0, 0, 0, 0] });
            }
        };
        fake.BeforeConfirmation = m =>
        {
            if (m.Id == 0x80 && stopAcknowledged)
            {
                monitoringEntered.Set();
                if (!confirmMonitoring.Wait(3000)) throw new TimeoutException("test monitoring confirmation gate");
                if (scenario == 2) throw new InvalidOperationException("later monitoring failure");
            }
        };
        service.ConfigureControlledStop(new([new(1, 0x2000, 0, [0])], 500));
        service.StartMonitoring(new(0x80, [0]), new(10, 2000, 5000));
        service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(100, 2000, 5000));
        await Until(() => fake.Sent.Any(m => m.Id == 0x201));

        // Pause only this waiter's captured ExecutionContext on asynchronous resumption.
        // The scheduler was started earlier and continues independently into Monitoring.
        var pause = new AsyncLocal<bool>(args =>
        {
            if (args.ThreadContextChanged && args.CurrentValue)
            {
                waiterPaused.Set();
                if (!resumeWaiter.Wait(3000)) throw new TimeoutException("test stop waiter gate");
            }
        });
        Task stopping;
        pause.Value = true;
        try { stopping = service.StopTargetsAsync(2500); }
        finally { pause.Value = false; }
        try
        {
            Assert.True(waiterPaused.Wait(2000));
            Assert.True(monitoringEntered.Wait(2000));
            var status = service.Snapshot().Cycle;
            Assert.Equal("Monitoring", status.State);
            Assert.False(status.MotionActive);
            Assert.False(status.HostExited);
            Assert.True(status.SubmittedFrames > status.ConfirmedFrames);
            if (scenario == 1)
            {
                confirmMonitoring.Set();
                service.StartTargets([1], [new(0x201, [2])], new(0x80, [0]), new(100, 2000, 5000));
                resumeWaiter.Set();
                var oldError = await Assert.ThrowsAsync<InvalidOperationException>(() => stopping);
                Assert.Contains("旧代次", oldError.Message);
                Assert.True(service.Snapshot().Cycle.MotionActive);
                Assert.Null(connection.IsolationReason);
                await service.StopTargetsAsync();
            }
            else
            {
                if (scenario == 2)
                {
                    confirmMonitoring.Set();
                    await Until(() => service.Snapshot().Cycle.Error != null);
                }
                resumeWaiter.Set();
                await stopping.WaitAsync(TimeSpan.FromSeconds(2));
                if (scenario == 2)
                {
                    Assert.Contains("later monitoring failure", service.Snapshot().Cycle.Error!);
                    Assert.Throws<InvalidOperationException>(() => service.StartTargets([1], [new(0x201, [2])], new(0x80, [0]), new()));
                    await Assert.ThrowsAsync<InvalidOperationException>(() => service.StopTargetsAsync());
                }
                else Assert.Null(service.Snapshot().Cycle.Error);
            }
        }
        finally
        {
            resumeWaiter.Set(); confirmMonitoring.Set();
            try { await connection.CloseAsync(); }
            catch { /* Original assertion remains the test failure; cleanup is best effort. */ }
        }
    }

    private sealed class DelayedAdapter : FakeAdapter, ICanConfirmedTransmitter
    {
        public Action<CanMessage>? BeforeConfirmation { get; set; }
        public void WriteConfirmed(CanMessage frame, CancellationToken ct)
        {
            Write(frame); BeforeConfirmation?.Invoke(frame); ct.ThrowIfCancellationRequested();
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var started = Stopwatch.StartNew();
        while (!condition())
        {
            if (started.ElapsedMilliseconds > 2000) throw new TimeoutException("test condition");
            await Task.Delay(1);
        }
    }
}
