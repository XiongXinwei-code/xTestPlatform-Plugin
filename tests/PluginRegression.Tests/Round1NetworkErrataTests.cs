using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Ethernet;
using Ethernet.Models;
using Http.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round1NetworkErrataTests
{
    [Fact]
    public async Task Ignored_http_close_does_not_report_completed_after_warning_callback_cancels_the_caller()
    {
        var plugin = new Http.HttpClientClosePlugin();
        var ctx = DispatchProxy.Create<IExecutionContext, CancelLogContext>();
        var state = (CancelLogContext)(object)ctx;
        state.SetStep(plugin, plugin.CreateSerializer().CreateDefault());
        using var caller = new CancellationTokenSource(); state.Cancel = caller.Cancel;
        Assert.Equal(TestStatus.Aborted, (await plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token)).StepResult.Status);
    }

    [Fact]
    public async Task Http_primary_request_failure_and_cancel_callback_failure_are_both_preserved_in_actual_owned_task()
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new FaultingHandler(entered, fail)) { BaseAddress = new("http://probe.invalid/") };
        var owner = new HttpClientResource { Client = client };
        var waiting = owner.SendAsync(new(HttpMethod.Post, "write"), caller.Token);
        await entered.Task.WaitAsync(guard.Token);
        var sync = typeof(HttpClientResource).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        Task actual;
        lock (sync) actual = Assert.Single((HashSet<Task>)typeof(HttpClientResource).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        fail.TrySetResult();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => actual);
        Assert.Contains("primary-handler-error", error.ToString());
        Assert.Contains("cancel-callback-error", error.ToString());
        await owner.CloseAsync(1000, guard.Token);
    }

    [Fact]
    public async Task Invalid_http_creation_timeout_is_rejected_before_waiting_for_the_registration_gate()
    {
        var plugin = new Http.HttpClientCreatePlugin();
        var (ctx, _) = TestContext.Create(plugin, new Http.Models.HttpClientCreateSetting { TimeoutMs = -1 });
        var gate = HttpHelper.GetRegistryGate(ctx.Resources);
        await gate.WaitAsync();
        using var caller = new CancellationTokenSource(100);
        try { Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token)).StepResult.Status); }
        finally { gate.Release(); }
    }

    [Fact]
    public async Task Http_queued_worker_checks_original_cancellation_before_submitting_even_when_the_bridge_callback_is_delayed()
    {
        using var workerEntered = new ManualResetEventSlim(); using var releaseWorker = new ManualResetEventSlim();
        using var cancelEntered = new ManualResetEventSlim(); using var releaseCancel = new ManualResetEventSlim();
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        int once = 0;
        var ambient = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.CurrentValue && Interlocked.Exchange(ref once, 1) == 0)
            { workerEntered.Set(); releaseWorker.Wait(10000); }
        });
        var handler = new CountingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("http://probe.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
        var owner = new HttpClientResource { Client = client };
        ambient.Value = true;
        var response = owner.SendAsync(new(HttpMethod.Post, "write"), caller.Token);
        ambient.Value = false;
        Assert.True(await Task.Run(() => workerEntered.Wait(3000)));
        using var registration = caller.Token.Register(() => { cancelEntered.Set(); releaseCancel.Wait(10000); });
        Task cancelling;
        using (ExecutionContext.SuppressFlow()) cancelling = Task.Run(caller.Cancel);
        try
        {
            Assert.True(await Task.Run(() => cancelEntered.Wait(3000)));
            releaseWorker.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.WaitAsync(guard.Token));
            Assert.Equal(0, handler.Calls);
        }
        finally { releaseWorker.Set(); releaseCancel.Set(); await cancelling.WaitAsync(guard.Token); await owner.CloseAsync(1000, guard.Token); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public async Task Udp_expected_datagram_length_is_not_a_silently_ignored_setting(int length)
    {
        using var reserved = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reserved.Client.LocalEndPoint!).Port; reserved.Close();
        using var peer = new UdpClient();
        var plugin = new UdpReceivePlugin();
        var (ctx, state) = TestContext.Create(plugin, new UdpReceiveSetting { LocalPort = port, TimeoutMs = 2000, ExpectedLength = 8, ResultVariable = "Locals.rx" });
        var execution = plugin.CreateExecutor().ExecuteAsync(ctx);
        await peer.SendAsync(new byte[length], new IPEndPoint(IPAddress.Loopback, port));
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("8", result.StepResult.Error!.Message);
        Assert.Empty(state.Variables);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closing_a_missing_connection_does_not_report_completed_if_the_log_callback_requests_abort(bool doip)
    {
        IStepPlugin plugin = doip ? new Ethernet.DoIP.DoipDisconnectPlugin() : new TcpClosePlugin();
        var ctx = DispatchProxy.Create<IExecutionContext, CancelLogContext>();
        var state = (CancelLogContext)(object)ctx;
        state.SetStep(plugin, plugin.CreateSerializer().CreateDefault());
        using var caller = new CancellationTokenSource();
        state.Cancel = caller.Cancel;
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token);
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }
    private sealed class FaultingHandler(TaskCompletionSource entered, TaskCompletionSource fail) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.Register(() => throw new InvalidOperationException("cancel-callback-error"));
            entered.TrySetResult();
            await fail.Task;
            throw new IOException("primary-handler-error");
        }
    }
}

public class CancelLogContext : TestContext
{
    public Action? Cancel { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Log") Cancel?.Invoke();
        return base.Invoke(method, args);
    }
}
