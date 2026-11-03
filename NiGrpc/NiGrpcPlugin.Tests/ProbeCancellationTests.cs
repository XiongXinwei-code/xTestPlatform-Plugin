using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class ProbeCancellationTests
{
    [Fact]
    public async Task Probe_is_not_submitted_when_scope_already_stopped()
    {
        var invoker = new DelayedProbeInvoker();
        using var wire = new GrpcNiWire(invoker);
        await Assert.ThrowsAsync<LocalInputException>(() => wire.ProbeAsync(1000, default, Task.CompletedTask));
        Assert.False(invoker.Started.Task.IsCompleted);
    }
    [Fact]
    public async Task Noncooperative_probe_times_out_and_requests_transport_cancel()
    {
        var invoker = new DelayedProbeInvoker();
        using var wire = new GrpcNiWire(invoker);
        var probe = wire.ProbeAsync(40, default);
        await invoker.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<TimeoutException>(() => probe);
        Assert.True(invoker.Token.IsCancellationRequested);
        invoker.Response.SetResult();
    }

    [Fact]
    public async Task Caller_cancel_does_not_run_probe_callback_on_stopping_thread()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var invoker = new DelayedProbeInvoker { Callback = () => { entered.Set(); release.Wait(); } };
        using var wire = new GrpcNiWire(invoker);
        using var caller = new CancellationTokenSource();
        try
        {
            var probe = wire.ProbeAsync(1000, caller.Token);
            await invoker.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Run(caller.Cancel).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
            invoker.Response.SetResult();
        }
        finally { release.Set(); }
    }

    private sealed class DelayedProbeInvoker : CallInvoker
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token;
        internal Action? Callback;
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest,TResponse>(Method<TRequest,TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Token = options.CancellationToken;
            if (Callback != null) Token.Register(Callback);
            Started.TrySetResult();
            async Task<TResponse> Reply() { await Response.Task; return (TResponse)(object)new Int32Value { Value = 1 }; }
            return new(Reply(), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest,TResponse>(Method<TRequest,TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest,TResponse> AsyncClientStreamingCall<TRequest,TResponse>(Method<TRequest,TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest,TResponse>(Method<TRequest,TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest,TResponse> AsyncDuplexStreamingCall<TRequest,TResponse>(Method<TRequest,TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
