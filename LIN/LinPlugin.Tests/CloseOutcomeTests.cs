using System.IO;
using System.Reflection;
using LIN.Adapters;
using LIN.Executors;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

[Collection("LIN managed retirement")]
public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task Historical_worker_failure_does_not_fail_confirmed_connection_close()
    {
        var driver = new Driver();
        var owner = new LinConnectionResource(driver);
        var job = owner.StartJob([_ => Task.FromException(new IOException("earlier write failed"))]);
        await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));

        await owner.CloseAsync(2000);
        await owner.CloseAsync(2000);

        Assert.True(owner.IsClosed);
        Assert.Equal(1, driver.Closes);
        Assert.NotNull(job.Error);
    }

    [Fact]
    public async Task Close_step_completes_and_preserves_worker_failure_as_warning()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var driver = new Driver();
        var owner = new LinConnectionResource(driver);
        var job = owner.StartJob([_ => Task.FromException(new IOException("earlier cyclic failure"))]);
        await job.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        var key = LinHelper.GetAdapterKey("LIN1");
        registry.Set(key, owner);
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>();
        var state = (SdkLinContext)(object)context;
        state.Registry = registry;
        state.Plugin = new LinClosePlugin();
        state.Setting = new LinCloseSetting { ConnectionName = "\"LIN1\"", TimeoutMs = 10000 };
        var logs = new List<string>();
        state.OnLog = text => logs.Add(text);

        var result = await new LinCloseExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.False(registry.TryGet<ILinAdapter>(key, out _));
        Assert.Contains(logs, line => line.Contains("Warn") && line.Contains("earlier cyclic failure"));
        Assert.Equal(1, driver.Closes);
    }

    private sealed class Driver : ILinAdapter
    {
        internal int Closes;
        public bool IsConnected => Closes == 0;
        public void Open(LinAdapterConfig config) { }
        public void Close() => Interlocked.Increment(ref Closes);
        public void Dispose() => Close();
        public void Write(LinFrame frame) { }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}
