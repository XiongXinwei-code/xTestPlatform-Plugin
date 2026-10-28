using System.Reflection;
using CAN.Adapters.Vector;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

public sealed class VectorFinalReleaseTests
{
    [Fact]
    public async Task Deactivate_error_does_not_skip_close_port_which_also_deactivates_channels()
    {
        int ports = 0, drivers = 0;
        var raw = new VectorAdapter((_, _) => 7, _ => { ports++; return 0; }, () => { drivers++; return 0; });
        Set(raw, "_isConnected", true); Set(raw, "_portHandle", 7); Set(raw, "_driverOpened", true);
        string key = Guid.NewGuid().ToString(); var owner = new CanConnectionResource(raw, key);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync());
            Assert.Contains("DeactivateChannel", error.ToString());
            Assert.Equal(1, ports); Assert.Equal(1, drivers);
            Assert.True(raw.HandlesReleased); Assert.True(owner.IsClosed);
            await CanConnectionResource.WaitForPreviousOwnerAsync(key, 1000);
        }
        finally
        {
            if (!raw.HandlesReleased)
            {
                Set(raw, "_isConnected", false);
                await owner.CloseAsync();
            }
        }
    }

    [Fact]
    public void Close_port_failure_keeps_port_and_driver_lease_until_real_retry_succeeds()
    {
        bool failPort = true; int drivers = 0;
        var raw = new VectorAdapter((_, _) => 7, _ => failPort ? (short)8 : (short)0, () => { drivers++; return 0; });
        Set(raw, "_isConnected", true); Set(raw, "_portHandle", 7); Set(raw, "_driverOpened", true);
        try
        {
            var error = Assert.ThrowsAny<Exception>(raw.Close);
            Assert.Contains("DeactivateChannel", error.ToString()); Assert.Contains("ClosePort", error.ToString());
            Assert.False(raw.HandlesReleased); Assert.Equal(0, drivers);
        }
        finally
        {
            failPort = false;
            Set(raw, "_isConnected", false);
            try { raw.Close(); } catch (InvalidOperationException) when (raw.HandlesReleased) { }
        }
        Assert.True(raw.HandlesReleased); Assert.Equal(1, drivers);
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
