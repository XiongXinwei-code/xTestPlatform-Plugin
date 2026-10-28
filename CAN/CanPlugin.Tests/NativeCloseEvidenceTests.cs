using System.Reflection;
using CAN.Adapters;
using CAN.Adapters.Peak;
using CAN.Adapters.Vector;
using CAN.Adapters.Tosun;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

public sealed class NativeCloseEvidenceTests
{
    private static void Field(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    [Theory]
    [InlineData("PEAK")]
    [InlineData("Vector")]
    [InlineData("TOSUN")]
    public async Task FailedNativeReleaseMustKeepPhysicalOwnerUntilRetry(string vendor)
    {
        bool fail = true; int attempts = 0;
        uint Release() { attempts++; return fail ? 1u : 0; }
        ICanAdapter raw;
        if (vendor == "PEAK")
        {
            raw = new PcanAdapter(_ => Release()); Field(raw, "_isConnected", true);
        }
        else if (vendor == "Vector")
        {
            raw = new VectorAdapter((_, _) => 0, _ => (short)Release(), () => 0);
            // Exact partial-open state: port obtained, configuration failed before activation.
            Field(raw, "_portHandle", 7); Field(raw, "_driverOpened", true);
        }
        else
        {
            raw = new TosunAdapter(_ => Release(), () => { });
            // Exact partial-open state: Connect obtained a handle; bitrate setup then failed.
            Field(raw, "_deviceHandle", (nuint)7); Field(raw, "_libInitialized", true);
        }
        string key = Guid.NewGuid().ToString(); var owner = new CanConnectionResource(raw, key);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync());
            Assert.Equal(1, attempts); Assert.False(owner.IsClosed);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new FakeAdapter(), key));
            fail = false; await owner.CloseAsync(); Assert.True(owner.IsClosed); Assert.Equal(2, attempts);
        }
        finally { fail = false; await owner.CloseAsync(); }
    }
}
