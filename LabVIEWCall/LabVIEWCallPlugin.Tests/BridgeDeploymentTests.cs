using System.Diagnostics;
using System.IO;
using System.Text;
using LabVIEWCallPlugin.BridgeProtocol;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public class BridgeDeploymentTests
{
    [Fact]
    public async Task DeployedWinExeStartsWithoutConsoleAndExitsOnInputEof()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "bridge", "LabVIEWCall.BridgeHost.exe");
        Assert.True(File.Exists(path));
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(path)!
        };
        using var child = Process.Start(start)!;
        var diagnostics = child.StandardError.ReadToEndAsync();
        try
        {
            var ready = await Task.Run(() => BridgeWire.Read<BridgeResponse>(child.StandardOutput.BaseStream))
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(ready);
            Assert.Equal("Ready", ready.Kind);
            Assert.Equal(child.Id, ready.ProcessId);
            child.StandardInput.Close();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, child.ExitCode);
            Assert.Equal("", await diagnostics);
        }
        finally
        {
            child.StandardInput.Close();
            // This test only starts the IPC loop and never sends a native request.
            if (!child.HasExited) child.Kill();
        }
    }
}
