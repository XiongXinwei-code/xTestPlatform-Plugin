using System.Net;
using System.Net.Http;
using System.Reflection;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpLifecycleTests
{
    [Fact]
    public async Task Cancelling_a_request_does_not_run_a_handler_callback_synchronously_on_the_stopping_thread()
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new CallbackHandler(token =>
        {
            token.Register(() => { callbackEntered.TrySetResult(); releaseCallback.Wait(10000); });
            requestEntered.TrySetResult();
            return response.Task;
        })) { BaseAddress = new("http://loopback.invalid/") };
        var owner = new HttpClientResource { Client = client };
        var waiting = owner.SendAsync(new(HttpMethod.Get, "read"), caller.Token);
        await requestEntered.Task.WaitAsync(guard.Token);
        var cancelling = Task.Run(caller.Cancel);
        try
        {
            await callbackEntered.Task.WaitAsync(guard.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromMilliseconds(500)));
            Assert.Same(cancelling, await Task.WhenAny(cancelling, Task.Delay(500, guard.Token)));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(50, guard.Token));
            Assert.False(owner.IsClosed);
        }
        finally
        {
            releaseCallback.Set();
            response.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("late") });
            await cancelling.WaitAsync(guard.Token);
            await owner.CloseAsync(1000, guard.Token);
        }
    }

    private sealed class CallbackHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(ct);
    }

    [Fact]
    public async Task Cancelled_noncooperative_request_does_not_claim_close_until_actual_exit_then_first_recreate_succeeds()
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var handler = new GatedHandler();
        var client = new HttpClient(handler) { BaseAddress = new("http://loopback.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
        var owner = new HttpClientResource { Client = client, BaseUrl = client.BaseAddress.ToString() };
        var plugin = new HttpRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpRequestSetting { Path = "\"read\"", ResponseVariable = "Locals.rx" });
        state.Items["Http.Client.Mes"] = owner;
        var execution = plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token);
        await handler.Entered.Task.WaitAsync(guard.Token);
        caller.Cancel();
        Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(guard.Token)).StepResult.Status);
        state.SetStep(new HttpClientClosePlugin(), new HttpClientCloseSetting { TimeoutMs = 50 });
        var closing = await new HttpClientClosePlugin().CreateExecutor().ExecuteAsync(ctx, guard.Token);
        Assert.Equal(TestStatus.Error, closing.StepResult.Status);
        Assert.Same(owner, state.Items["Http.Client.Mes"]);
        Assert.False(owner.IsClosed);
        handler.Response.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("late") });
        await owner.CloseAsync(1000, guard.Token);
        Assert.True(owner.IsClosed);
        Assert.Empty(state.Variables);
        state.SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { ReplaceIfExists = true });
        Assert.Equal(TestStatus.Completed, (await new HttpClientCreatePlugin().CreateExecutor().ExecuteAsync(ctx, guard.Token)).StepResult.Status);
        var current = Assert.IsType<HttpClientResource>(state.Items["Http.Client.Mes"]);
        Assert.NotSame(owner, current);
        await current.CloseAsync(1000, guard.Token);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Response_continuation_cannot_publish_after_cancellation_or_exact_owner_close(bool soap, bool closeOwner)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var handler = new GatedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("http://loopback.invalid/"), Timeout = TimeSpan.FromSeconds(10) };
        var owner = new HttpClientResource { Client = client, BaseUrl = client.BaseAddress.ToString() };
        var plugin = soap ? (xTestPlatform.Core.Plugins.Contracts.IStepPlugin)new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, soap
            ? new HttpSoapRequestSetting { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", ResponseVariable = "Locals.rx" }
            : new HttpRequestSetting { Path = "\"read\"", ResponseVariable = "Locals.rx" });
        state.Items["Http.Client.Mes"] = owner;
        var dispatcher = new PausedContinuationContext();
        var previous = SynchronizationContext.Current;
        Task<ExecutionResult> execution;
        try
        {
            SynchronizationContext.SetSynchronizationContext(dispatcher);
            execution = plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        await handler.Entered.Task.WaitAsync(guard.Token);
        dispatcher.Pause();
        handler.Response.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("<ok/>") });
        await dispatcher.Posted.Task.WaitAsync(guard.Token);
        if (closeOwner)
        {
            owner.Dispose();
            state.Items["Http.Client.Mes"] = new HttpClientResource { Client = new HttpClient(), BaseUrl = "http://new.invalid/" };
        }
        else caller.Cancel();
        dispatcher.Release();
        var result = await execution.WaitAsync(guard.Token);
        Assert.Equal(closeOwner ? TestStatus.Error : TestStatus.Aborted, result.StepResult.Status);
        Assert.Empty(state.Variables);
        foreach (var item in state.Items.Values.OfType<IDisposable>()) item.Dispose();
    }

    private sealed class GatedHandler : HttpMessageHandler
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<HttpResponseMessage> Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Entered.TrySetResult(); return Response.Task; }
    }

    private sealed class PausedContinuationContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback, object?)> _queue = new();
        private bool _released;
        private bool _paused;
        internal readonly TaskCompletionSource Posted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_queue)
            {
                if (_paused && !_released) { _queue.Enqueue((callback, state)); Posted.TrySetResult(); return; }
            }
            Run(callback, state);
        }
        internal void Pause() { lock (_queue) _paused = true; }
        private void Run(SendOrPostCallback callback, object? state) => ThreadPool.QueueUserWorkItem(_ =>
        {
            var previous = Current;
            try { SetSynchronizationContext(this); callback(state); }
            finally { SetSynchronizationContext(previous); }
        });
        internal void Release()
        {
            lock (_queue)
            {
                _released = true;
                while (_queue.TryDequeue(out var item)) Run(item.Item1, item.Item2);
            }
        }
    }
}
