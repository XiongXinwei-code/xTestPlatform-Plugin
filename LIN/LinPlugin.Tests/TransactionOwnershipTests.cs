using System.Collections.Concurrent;
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

public sealed class TransactionOwnershipTests
{
    [Fact]
    public async Task WriteReadOwnsBothOperationsAcrossTheLoggingGap()
    {
        var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = "LIN_TRANSACTION_" + Guid.NewGuid().ToString("N") });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        IExecutionContext Context(string data)
        {
            var ctx = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)ctx;
            state.Plugin = new LinWriteReadPlugin();
            state.Setting = new LinWriteReadSetting { ConnectionName = "\"L\"", FrameId = "\"0x12\"", Data = $"\"{data}\"", ResponseTimeoutMs = 2000 };
            state.Items[LinHelper.GetAdapterKey("L")] = owner;
            if (data == "01") state.OnLog = text => { if (text.Contains("LIN 发送")) { entered.TrySetResult(); release.Wait(); } };
            return ctx;
        }
        var first = Task.Run(() => new LinWriteReadExecutor().ExecuteAsync(Context("01")));
        Task<ExecutionResult>? second = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second = Task.Run(() => new LinWriteReadExecutor().ExecuteAsync(Context("02")));
            await Task.Delay(100);
            Assert.Equal(1, raw.Writes);
            release.Set();
            var a = await first; var b = await second;
            Assert.Equal(TestStatus.Completed, a.StepResult.Status); Assert.Equal(TestStatus.Completed, b.StepResult.Status);
            Assert.Contains("01", a.StepResult.Value?.ToString()); Assert.Contains("02", b.StepResult.Value?.ToString());
        }
        finally { release.Set(); await first; if (second != null) await second; await owner.CloseAsync(); }
    }

    private sealed class Driver : ILinAdapter
    {
        private readonly ConcurrentQueue<LinFrame> _replies = new();
        public int Writes;
        public bool IsConnected { get; private set; }
        public void Open(LinAdapterConfig config) => IsConnected = true;
        public void Write(LinFrame frame) { Interlocked.Increment(ref Writes); _replies.Enqueue(new() { FrameId = frame.FrameId, Data = frame.Data.ToArray() }); }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => _replies.TryDequeue(out var frame) ? frame : null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        public void Close() => IsConnected = false;
        public void Dispose() => Close();
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}
