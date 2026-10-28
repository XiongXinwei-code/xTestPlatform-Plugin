using System.Diagnostics;
using VISA.Helpers;
using Xunit;

namespace VISA.Tests;

public sealed class RegistrationBudgetTests
{
    [Fact]
    public async Task CloseInvalidatesAnOlderOpenWithoutWaitingForRegistrationGate()
    {
        var registry = new object(); const string key = "V";
        var gate = VisaHelper.GetRegistrationLock(registry, key);
        await gate.WaitAsync();
        try
        {
            var generation = VisaHelper.GetCloseGeneration(registry, key);
            VisaHelper.MarkCloseRequested(registry, key);
            Assert.Throws<InvalidOperationException>(() => VisaHelper.ThrowIfOpenSuperseded(registry, key, generation));
            VisaHelper.ThrowIfOpenSuperseded(registry, key, VisaHelper.GetCloseGeneration(registry, key));
        }
        finally { gate.Release(); }
    }

    [Fact]
    public void SpentRegistrationBudgetCannotBecomeAnotherOneMillisecondNativeAttempt() =>
        Assert.Throws<TimeoutException>(() => VisaHelper.RemainingBudget(Stopwatch.GetTimestamp() - Stopwatch.Frequency, 1));

    [Fact]
    public void OldUnownedOpenRejectsBeforeCallingAnyDriver()
    {
#pragma warning disable CS0618
        Assert.Throws<NotSupportedException>(() => VisaHelper.OpenSession("not a native resource", 1000, 1000, "\\n"));
#pragma warning restore CS0618
    }
}
