using System.Diagnostics;
using OpcUa.Helpers;
using Xunit;

namespace OpcUa.Tests;

public sealed class RegistrationBudgetTests
{
    [Fact]
    public async Task CloseInvalidatesAnOlderOpenWithoutWaitingForRegistrationGate()
    {
        var registry = new object(); const string key = "C";
        var gate = OpcUaHelper.GetRegistrationLock(registry, key);
        await gate.WaitAsync();
        try
        {
            var generation = OpcUaHelper.GetCloseGeneration(registry, key);
            OpcUaHelper.MarkCloseRequested(registry, key);
            Assert.Throws<InvalidOperationException>(() => OpcUaHelper.ThrowIfOpenSuperseded(registry, key, generation));
            OpcUaHelper.ThrowIfOpenSuperseded(registry, key, OpcUaHelper.GetCloseGeneration(registry, key));
        }
        finally { gate.Release(); }
    }

    [Fact]
    public void SpentRegistrationBudgetCannotBecomeAnotherOneMillisecondNativeAttempt() =>
        Assert.Throws<TimeoutException>(() => OpcUaHelper.RemainingBudget(Stopwatch.GetTimestamp() - Stopwatch.Frequency, 1));
}
