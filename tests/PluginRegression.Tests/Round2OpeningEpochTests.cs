using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.Models;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round2OpeningEpochTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_waiting_for_registration_cannot_publish_after_sdk_scope_cleanup_without_caller_cancellation(bool newDebugContext)
    {
        using var guard = new CancellationTokenSource(10000);
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = resources;
        var plugin = new HttpClientCreatePlugin();
        state.SetStep(plugin, new HttpClientCreateSetting { TimeoutMs = 3000, BaseUrl = "\"http://example.invalid/\"" });
        var gate = HttpHelper.GetRegistryGate(resources);
        await gate.WaitAsync(guard.Token);
        Task<ExecutionResult> opening;
        try
        {
            opening = plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token);
            Assert.False(opening.IsCompleted);
            SdkResourceLifecycleTests.Cleanup(resources, newDebugContext ? "CleanupAll" : "CleanupExecution");
        }
        finally { gate.Release(); }
        try
        {
            var result = await opening.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(resources.Contains("Http.Client.Mes"));
            if (newDebugContext) state.Registry = SdkResourceLifecycleTests.NewRegistry();
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        }
        finally { SdkResourceLifecycleTests.Cleanup(state.Registry, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Network_open_captures_execution_before_async_expression_evaluation(bool cleanupAll, bool doip)
    {
        using var guard = new CancellationTokenSource(10000);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = resources;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Evaluate(string expression, IExecutionContext context)
        {
            if (expression != "test-host") return await EthernetExecutorHelper.EvalStringAsync(expression, context);
            entered.TrySetResult();
            await release.Task.WaitAsync(guard.Token);
            return "127.0.0.1";
        }
        Task<ExecutionResult> opening;
        if (doip)
        {
            state.SetStep(new DoipConnectPlugin(), new DoipConnectSetting { SessionName = "\"epoch-doip\"", RemoteHost = "test-host",
                RemotePort = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(), TimeoutMs = 1000 });
            opening = new Ethernet.DoIP.Executors.DoipConnectExecutor(Evaluate).ExecuteAsync(ctx, guard.Token);
        }
        else
        {
            state.SetStep(new TcpOpenPlugin(), new TcpOpenSetting { ConnectionName = "\"epoch-tcp\"", RemoteHost = "test-host",
                RemotePort = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(), ConnectTimeoutMs = 1000 });
            opening = new Ethernet.Executors.TcpOpenExecutor(Evaluate).ExecuteAsync(ctx, guard.Token);
        }
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(resources, cleanupAll ? "CleanupAll" : "CleanupExecution");
            release.TrySetResult();
            Assert.Equal(TestStatus.Error, (await opening.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(listener.Pending(), "An old step must not submit a TCP connect after its execution scope was retired");
        }
        finally { release.TrySetResult(); await opening.WaitAsync(guard.Token); SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); }
    }

    [Fact]
    public async Task Ethernet_null_expression_does_not_turn_into_source_text()
    {
        var (ctx, _) = TestContext.Create(new TcpSendPlugin(), new TcpSendSetting());
        Assert.Equal(string.Empty, await EthernetExecutorHelper.EvalStringAsync("null", ctx));
        Assert.Equal("null", await EthernetExecutorHelper.EvalStringAsync("\"null\"", ctx));
        Assert.Equal("0", await EthernetExecutorHelper.EvalStringAsync("0", ctx));
    }
}
