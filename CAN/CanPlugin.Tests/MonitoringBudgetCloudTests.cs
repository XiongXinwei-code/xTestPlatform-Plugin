using System.Diagnostics;
using CAN.Runtime;
using Xunit;

namespace CAN.ContractTests;

[Collection("CAN blocked I/O")]
public sealed class MonitoringBudgetCloudTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Monitoring_uses_its_confirmation_deadline_while_motion_keeps_its_existing_period_guard(bool motion)
    {
        var raw = new CAN.Tests.ConfirmedAdapter { OnWrite = _ => Thread.Sleep(12) };
        var connection = new CanConnectionResource(raw);
        var service = connection.StartService("cloud-budget", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            // Wide, deterministic interval: native fake completion takes >1ms and
            // <1000ms. No assertion depends on reproducing precisely 20.0622ms.
            if (motion) service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(1, 1000, 1000));
            else service.StartMonitoring(new(0x80, [0]), new(1, 1000, 1000));
            using var guard = new CancellationTokenSource(10000);
            CycleStatus cycle;
            while (true)
            {
                // Capture one immutable snapshot. A later batch may already be
                // submitted when the previous cycle count becomes observable.
                cycle = service.Snapshot().Cycle;
                if (cycle.Error != null || (cycle.CompletedCycles >= 2 &&
                    cycle.SubmittedFrames == cycle.ConfirmedFrames && cycle.Timing?.TotalElapsedMs != null)) break;
                await Task.Delay(2, guard.Token);
            }
            Assert.True(cycle.LastDurationMs > 1);
            Assert.Equal(cycle.SubmittedFrames, cycle.ConfirmedFrames);
            var timing = Assert.IsType<CycleTimingStatus>(cycle.Timing);
            Assert.Equal(motion ? "RPDO/SYNC" : "MonitoringSYNC", timing.Phase);
            Assert.Equal(1, timing.PeriodMs);
            Assert.Equal(1000, timing.SendTimeoutMs);
            Assert.Equal(1000, timing.MaxStartLatenessMs);
            Assert.True(timing.TotalElapsedMs > 1);
            Assert.True(timing.DispatchDelayMs >= 0);
            Assert.True(timing.AdapterBatchMs > 1);
            Assert.True(timing.ContinuationDelayMs >= 0);
            if (motion) Assert.Contains("发送周期执行超限", cycle.Error);
            else
            {
                Assert.Null(cycle.Error);
                Assert.True(cycle.MonitoringSyncActive);
                Assert.True(cycle.CompletedCycles >= 2);
                Assert.True(cycle.SlowMonitoringBatches >= 2);
            }
        }
        finally
        {
            try { await connection.CloseAsync(5000); }
            catch (Exception) when (connection.IsClosed) { } // The historical motion fault remains observable.
            Assert.True(connection.IsClosed);
        }
    }
}
