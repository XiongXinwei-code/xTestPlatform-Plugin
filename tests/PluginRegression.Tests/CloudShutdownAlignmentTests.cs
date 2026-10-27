using Http;
using Http.Executors;
using Http.Helpers;
using Http.Models;
using System.Net;
using System.Net.Http;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class CloudShutdownAlignmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slow_client_builder_serializes_its_own_name_but_does_not_block_another_clients_close(bool sameName)
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseBuilder = new ManualResetEventSlim();
        var builderEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createPlugin = new HttpClientCreatePlugin();
        var (context, state) = TestContext.Create(createPlugin, new HttpClientCreateSetting { ClientName = "\"A\"", TimeoutMs = 10000 });
        var other = new HttpClientResource { Client = new HttpClient() };
        state.Items[HttpHelper.GetClientKey("B")] = other;
        var creator = new HttpClientCreateExecutor((build, timeout, caller, stop) => HttpHelper.BuildClientAsync(() =>
        {
            builderEntered.TrySetResult();
            releaseBuilder.Wait(guard.Token);
            return build();
        }, timeout, caller, stop));
        var creating = creator.ExecuteAsync(context, guard.Token);
        try
        {
            await builderEntered.Task.WaitAsync(guard.Token);
            var closePlugin = new HttpClientClosePlugin();
            state.SetStep(closePlugin, new HttpClientCloseSetting { ClientName = sameName ? "\"A\"" : "\"B\"", TimeoutMs = sameName ? 10000 : 1000 });
            var closing = closePlugin.CreateExecutor().ExecuteAsync(context, guard.Token);
            if (sameName)
            {
                await Task.WhenAny(closing, Task.Delay(50, guard.Token));
                Assert.False(closing.IsCompleted);
                releaseBuilder.Set();
                Assert.Equal(TestStatus.Completed, (await creating.WaitAsync(guard.Token)).StepResult.Status);
                Assert.Equal(TestStatus.Completed, (await closing.WaitAsync(guard.Token)).StepResult.Status);
                Assert.False(state.Items.ContainsKey(HttpHelper.GetClientKey("A")));
            }
            else
            {
                Assert.Equal(TestStatus.Completed, (await closing.WaitAsync(guard.Token)).StepResult.Status);
                Assert.True(other.IsClosed);
                Assert.False(creating.IsCompleted);
            }
        }
        finally
        {
            releaseBuilder.Set();
            await creating.WaitAsync(guard.Token);
            foreach (var owner in state.Items.Values.OfType<HttpClientResource>()) await owner.CloseAsync(2000, guard.Token);
            await other.CloseAsync(2000, guard.Token);
        }
    }

    [Fact]
    public async Task Successful_sdk_dispose_does_not_claim_close_until_the_actual_request_has_exited()
    {
        using var guard = new CancellationTokenSource(15000);
        var handler = new HeldResponseHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://test.invalid/") } };
        var sending = owner.SendAsync(new(HttpMethod.Get, "read"), default);
        await handler.SendEntered.Task.WaitAsync(guard.Token);
        var closing = owner.CloseAsync(5000, guard.Token);
        try
        {
            await handler.Disposed.Task.WaitAsync(guard.Token);
            Assert.False(owner.IsClosed);
            Assert.False(closing.IsCompleted);
            handler.Response.TrySetResult(new(HttpStatusCode.OK));
            await closing.WaitAsync(guard.Token);
            Assert.True(owner.IsClosed);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sending);
        }
        finally { handler.Response.TrySetResult(new(HttpStatusCode.OK)); await owner.CloseAsync(5000, guard.Token); }
    }

    [Fact]
    public async Task Failed_sdk_dispose_is_retained_and_a_second_noop_dispose_cannot_fake_release()
    {
        var handler = new FailingDisposeHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) };
        await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync(2000));
        Assert.False(owner.IsClosed);
        await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync(2000));
        Assert.False(owner.IsClosed);
        Assert.Equal(1, handler.DisposeCalls);
    }

    [Fact]
    public async Task Sdk_dispose_may_release_a_noncooperative_send_but_close_still_waits_for_dispose_to_return()
    {
        using var guard = new CancellationTokenSource(15000);
        using var releaseDispose = new ManualResetEventSlim();
        var handler = new DisposeReleasesSendHandler(releaseDispose, guard.Token);
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://test.invalid/"), Timeout = Timeout.InfiniteTimeSpan } };
        var sending = owner.SendAsync(new(HttpMethod.Get, "read"), CancellationToken.None);
        await handler.SendEntered.Task.WaitAsync(guard.Token);
        var closing = owner.CloseAsync(5000, guard.Token);
        try
        {
            await handler.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(owner.IsClosed);
            Assert.False(closing.IsCompleted);
            Assert.Equal(1, handler.DisposeCalls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sending);
            releaseDispose.Set();
            await closing.WaitAsync(guard.Token);
            Assert.True(owner.IsClosed);
            Assert.Equal(1, handler.DisposeCalls);
        }
        finally
        {
            releaseDispose.Set(); handler.Response.TrySetResult(new(HttpStatusCode.OK));
            await owner.CloseAsync(5000, guard.Token);
            try { await sending; } catch (InvalidOperationException) { }
        }
    }

    private sealed class DisposeReleasesSendHandler(ManualResetEventSlim releaseDispose, CancellationToken guard) : HttpMessageHandler
    {
        internal readonly TaskCompletionSource SendEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource DisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<HttpResponseMessage> Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DisposeCalls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { SendEntered.TrySetResult(); return Response.Task; }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref DisposeCalls);
                Response.TrySetResult(new(HttpStatusCode.OK) { Content = new StringContent("disposed") });
                DisposeEntered.TrySetResult();
                releaseDispose.Wait(guard);
            }
            base.Dispose(disposing);
        }
    }

    private sealed class HeldResponseHandler : HttpMessageHandler
    {
        internal readonly TaskCompletionSource SendEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<HttpResponseMessage> Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { SendEntered.TrySetResult(); return Response.Task; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed.TrySetResult(); base.Dispose(disposing); }
    }

    private sealed class FailingDisposeHandler : HttpMessageHandler
    {
        internal int DisposeCalls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        { if (disposing) Interlocked.Increment(ref DisposeCalls); throw new InvalidOperationException("fake handler release failure"); }
    }
}
