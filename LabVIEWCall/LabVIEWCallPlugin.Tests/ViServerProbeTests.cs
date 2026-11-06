using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public class ViServerProbeTests
{
    [Fact]
    public async Task ClosedPortProvidesConfigurationGuidanceWithoutNativeLoad()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ViServerProbe.CheckAsync(port, deadline.Token));
        Assert.Contains("VI Server", error.Message);
        Assert.Contains("TCP/IP", error.Message);
        Assert.Contains(port.ToString(), error.Message);
        Assert.IsType<SocketException>(error.InnerException);
    }

    [Fact]
    public async Task NonLabviewListenerIsRejectedWithoutSendingAnyRequest()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ViServerProbe.CheckAsync(((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token));
            Assert.Contains("非 LabVIEW", error.Message);
            Assert.Contains($"PID={Environment.ProcessId}", error.Message);
            Assert.False(listener.Pending());
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task CancelledProbeDoesNotConnect()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ViServerProbe.CheckAsync(3363, cancellation.Token));
    }

    [Fact]
    public void PlatformProcessCannotAccidentallyLoadNativeBridge()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LvBridgeLibrary.Resolve("lvLib.dll", typeof(LvLibHelper).Assembly, null));
        Assert.Contains("禁止在平台进程内加载", error.Message);
    }

    [Fact]
    public void OwnerSelectionUsesOnlyListeningLoopbackOrWildcardAtRequestedPort()
    {
        ViServerListener[] rows = [new(5, IPAddress.Loopback, 3363, 5),
            new(2, IPAddress.Parse("192.0.2.3"), 3363, 6), new(2, IPAddress.Loopback, 3364, 7),
            new(2, IPAddress.Any, 3363, 42), new(2, IPAddress.Loopback, 3363, 42)];
        Assert.Equal(42, ViServerProbe.SelectListenerOwner(rows, 3363));
        Assert.Throws<InvalidOperationException>(() => ViServerProbe.SelectListenerOwner(
            [new(2, IPAddress.Any, 3363, 42), new(2, IPAddress.Loopback, 3363, 43)], 3363));
    }

    [Fact]
    public void NativeTableDecoderPreservesNetworkByteOrderAndRejectsTruncatedRows()
    {
        var table = new byte[28];
        BinaryPrimitives.WriteUInt32LittleEndian(table, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(4), 2);
        IPAddress.Loopback.GetAddressBytes().CopyTo(table, 8);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(12), 3363);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(24), 42);
        var row = Assert.Single(ViServerProbe.DecodeOwnerTable(table));
        Assert.Equal(new ViServerListener(2, IPAddress.Loopback, 3363, 42), row);
        Assert.Throws<InvalidDataException>(() => ViServerProbe.DecodeOwnerTable(table.AsSpan(0, 27)));
    }

    [Fact]
    public void IdentityRequiresIdeButNotTargetBitnessAndRejectsPidReuseOrVersionSwitch()
    {
        var original = Identity();
        original.Validate();
        original.EnsureSame(original with { ImagePath = original.ImagePath.ToUpperInvariant() });
        Assert.Throws<InvalidOperationException>(() => (original with { ImagePath = @"C:\Tools\other.exe" }).Validate());
        (original with { Machine = 0x014c }).Validate();
        (original with { Machine = 0xAA64 }).Validate();
        Assert.Throws<InvalidOperationException>(() => original.EnsureSame(original with { Machine = 0x014c }));
        Assert.Throws<InvalidOperationException>(() => original.EnsureSame(original with { Port = 3364 }));
        Assert.Throws<InvalidOperationException>(() => original.EnsureSame(original with { ProcessId = 43 }));
        Assert.Throws<InvalidOperationException>(() => original.EnsureSame(original with { StartTimeUtc = original.StartTimeUtc.AddSeconds(1) }));
        Assert.Throws<InvalidOperationException>(() => original.EnsureSame(original with { FileVersion = "25.3.0.0" }));
        Assert.Contains("26.1.0.0", original.Diagnostic);
    }

    private static ViServerIdentity Identity() => new(3363, 42, new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc),
        @"C:\Program Files\National Instruments\LabVIEW 2026\LabVIEW.exe", "26.1.0.0", 0x8664);
}
