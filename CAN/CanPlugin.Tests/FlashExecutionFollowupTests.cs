using System.IO;
using System.Reflection;
using System.Text.Json;
using CAN.Flash;
using CAN.Flash.Executors;
using CAN.Flash.Models;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class FlashExecutionFollowupTests
{
    [WpfFact]
    public async Task File_preflight_never_runs_on_the_executor_callers_dispatcher()
    {
        var registry = NewRegistry(); int callerThread = Environment.CurrentManagedThreadId, fileThread = 0;
        using var release = new ManualResetEventSlim();
        var watchdog = Task.Run(async () => { await Task.Delay(250); release.Set(); });
        try
        {
            var executor = new CanFlashExecutor(_ => { fileThread = Environment.CurrentManagedThreadId; release.Wait(); return false; });
            var result = await executor.ExecuteAsync(Context(registry));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.NotEqual(callerThread, fileThread);
        }
        finally { release.Set(); await watchdog; Cleanup(registry); }
    }

    [WpfTheory]
    [InlineData("cancel")]
    [InlineData("retire")]
    public async Task A_blocked_readonly_preflight_cannot_publish_missing_file_from_an_old_execution(string transition)
    {
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = NewRegistry();
        var watchdog = Task.Run(async () =>
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (transition == "cancel") caller.Cancel(); else Assert.True(registry.Remove("PluginCommon.ExecutionEpoch:CAN"));
            await Task.Delay(250); release.Set();
        });
        try
        {
            var result = await new CanFlashExecutor(_ =>
            {
                entered.TrySetResult();
                try { release.Wait(); return false; } finally { exited.TrySetResult(); }
            }).ExecuteAsync(Context(registry), caller.Token);
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition == "cancel") Assert.False(exited.Task.IsCompleted, "只结束前台等待；不能冒充文件调用已退出");
            else Assert.Contains("执行资源作用域已回收", result.StepResult.Error!.Message);
        }
        finally { release.Set(); await watchdog; await exited.Task.WaitAsync(TimeSpan.FromSeconds(5)); Cleanup(registry); }
    }

    [Fact]
    public async Task Precancelled_flash_never_enters_file_preflight()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        var registry = NewRegistry(); int checks = 0;
        try
        {
            var result = await new CanFlashExecutor(_ => { checks++; return false; }).ExecuteAsync(Context(registry), caller.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status); Assert.Equal(0, checks);
        }
        finally { Cleanup(registry); }
    }

    [Theory]
    [InlineData(0, 512, 0)] [InlineData(1, 512, 0)] [InlineData(2, 512, 0)]
    [InlineData(0, 0, 0)] [InlineData(1, 0, 0)] [InlineData(2, 0, 0)]
    [InlineData(3, 512, 1)] [InlineData(8, 512, 5)] [InlineData(255, 2, 2)]
    [InlineData(8, 0, 5)] [InlineData(3, 0, 1)]
    public async Task Ecu_download_capacity_must_include_sid_and_counter_before_any_transfer(int ecuMaximum, int configured, int expectedChunk)
    {
        string path = Path.GetTempFileName();
        var registry = NewRegistry(); var raw = new FakeAdapter();
        var transfers = new List<byte[]>();
        raw.OnWrite = frame =>
        {
            var request = frame.Data.Skip(1).Take(frame.Data[0] & 0x0F).ToArray();
            byte[] response;
            if (request[0] == 0x34) response = [0x74, 0x10, (byte)ecuMaximum];
            else if (request[0] == 0x36) { transfers.Add(request); response = [0x76, request[1]]; }
            else if (request[0] == 0x37) response = [0x77];
            else throw new InvalidOperationException($"unexpected fake UDS SID {request[0]:X2}");
            var single = new byte[8]; single[0] = (byte)response.Length; response.CopyTo(single, 1);
            raw.Incoming.Add(new CanMessage { Id = 0x7E8, Data = single });
        };
        registry.Set(CanHelper.GetAdapterKey("CAN1"), raw, ResourceLifetime.Execution);
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);
            var setting = new CanFlashSetting { FilePath = JsonSerializer.Serialize(path), Format = FirmwareFormat.Binary,
                BaseAddress = "\"0x01\"", AddressAndLengthFormatId = "\"0x11\"", EraseBeforeDownload = false,
                CheckMode = FlashCheckMode.None, MaxBlockSize = configured, PreDownloadDelayMs = 0 };
            var context = Context(registry, setting);
            var result = await new CanFlashExecutor().ExecuteAsync(context);
            Assert.Contains(raw.Sent, frame => frame.Data[1] == 0x34); // Must reach real negotiated UDS capacity.
            if (expectedChunk == 0)
            {
                Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(transfers);
                Assert.DoesNotContain(raw.Sent, frame => frame.Data[1] == 0x37);
            }
            else
            {
                Assert.Equal(TestStatus.Completed, result.StepResult.Status);
                Assert.Equal(expectedChunk, transfers[0].Length - 2);
                Assert.Equal(5, transfers.Sum(frame => frame.Length - 2));
                Assert.All(transfers, frame => Assert.InRange(frame.Length, 3, Math.Min(ecuMaximum, configured == 0 ? int.MaxValue : configured + 2)));
            }
        }
        finally { Cleanup(registry); File.Delete(path); }
    }

    private static IExecutionContext Context(IResourceRegistry registry, CanFlashSetting? setting = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context; state.Registry = registry; state.Plugin = new CanFlashPlugin();
        state.Setting = setting ?? new CanFlashSetting { FilePath = "\"not-opened.bin\"", Format = FirmwareFormat.Binary };
        return context;
    }
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
        .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry) => registry.GetType()
        .GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
}
