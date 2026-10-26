using System.Reflection;
using CAN.Adapters;
using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CloseStepPhysicalOutcomeTests
{
    [Fact]
    public async Task Verified_physical_release_with_historical_error_completes_step_and_first_reopen()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        string channel = Random.Shared.Next(10000, int.MaxValue).ToString();
        string key = CanHelper.GetAdapterKey("CAN1");
        var raw = new Driver { ReportHistory = true }; var owner = new CanConnectionResource(raw, CanChannelIdentity.Normalize(CanAdapterType.Kvaser, channel));
        registry.Set(key, owner);
        var closeContext = Context(registry, new CanClosePlugin(), new CanCloseSetting { ConnectionName = "\"CAN1\"", TimeoutMs = 6000 });
        try
        {
            var result = await new CanCloseExecutor().ExecuteAsync(closeContext);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.True(owner.IsClosed); Assert.True(raw.HandlesReleased);
            Assert.Contains("historical driver stop failure", result.StepResult.Value?.ToString());
            Assert.Contains(((SdkCanContext)(object)closeContext).Logs, log => log.Contains("Warn") && log.Contains("historical driver stop failure"));
            Assert.False(registry.Contains(key));
            // Direct close remains a diagnostic API; the step distinguishes a
            // physically closed owner from unresolved SDK release.
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync());
            var opened = await new CanOpenExecutor(_ => new Driver()).ExecuteAsync(Context(registry, new CanOpenPlugin(),
                new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = $"\"{channel}\"", ConnectionName = "\"CAN1\"", TimeoutMs = 3000 }));
            Assert.Equal(TestStatus.Completed, opened.StepResult.Status);
        }
        finally
        {
            if (registry.TryGet<ICanAdapter>(key, out var current))
                try { await ((CanConnectionResource)current).CloseAsync(); }
                catch (Exception) when (((CanConnectionResource)current).IsClosed) { }
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry, xTestPlatform.Core.Plugins.Contracts.IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context; state.Registry = registry; state.Plugin = plugin; state.Setting = setting;
        return context;
    }
    private sealed class Driver : ICanAdapter, ICanCloseState
    {
        internal bool ReportHistory;
        public bool HandlesReleased { get; private set; }
        public bool IsConnected => !HandlesReleased;
        public void Open(CanAdapterConfig config) { }
        public void Close() { HandlesReleased = true; if (ReportHistory) throw new InvalidOperationException("historical driver stop failure"); }
        public void Dispose() { }
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
