using OpcUa.Helpers;
using OpcUa.Models;
using System.IO;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class CsvPartialExportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialExportRetainsFifoAndBlocksSamePathEvenForAnotherAcquisition(bool replaceOwner)
    {
        string failedPath = Path.Combine(Path.GetTempPath(), $"opc-partial-{Guid.NewGuid():N}.csv");
        string newPath = Path.Combine(Path.GetTempPath(), $"opc-recovered-{Guid.NewGuid():N}.csv");
        var (original, ready) = AcquisitionBudgetTests.CreateOneRecord();
        OpcUaDataAcqTask? replacement = null;
        int writes = 0;
        Task Export(string path, IReadOnlyList<OpcUaDataAcqItem> _, IReadOnlyList<DataAcqRecord> records)
        {
            Interlocked.Increment(ref writes);
            File.AppendAllText(path, path == failedPath ? "partially-written-record" : $"complete-sequence-{records[0].Sequence}");
            if (path == failedPath) throw new IOException("injected failure after partial CSV append");
            return Task.CompletedTask;
        }
        original.CsvExporter = Export;
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            var first = await Read(original, failedPath);
            Assert.Equal(TestStatus.Error, first.StepResult.Status);
            Assert.Contains("injected failure", first.StepResult.Error!.Detail);
            Assert.Equal(1, original.SampleCount);
            Assert.Equal(0, original.LastExportCommitSequence);
            Assert.Equal("partially-written-record", File.ReadAllText(failedPath));

            var current = original;
            if (replaceOwner)
            {
                var next = AcquisitionBudgetTests.CreateOneRecord(); replacement = next.Task;
                await next.Ready.WaitAsync(TimeSpan.FromSeconds(3)); replacement.CsvExporter = Export; current = replacement;
            }
            var retry = await Read(current, failedPath);
            Assert.Equal(TestStatus.Error, retry.StepResult.Status);
            Assert.Equal(1, writes);
            Assert.Contains("写入结果未知", first.StepResult.Error.Message);
            Assert.Contains("隔离", retry.StepResult.Error!.Message);
            Assert.Equal(1, current.SampleCount);
            Assert.Equal("partially-written-record", File.ReadAllText(failedPath));

            var recovery = await Read(current, newPath);
            Assert.Equal(TestStatus.Completed, recovery.StepResult.Status);
            Assert.Equal(2, writes); Assert.Equal(0, current.SampleCount);
            Assert.Equal(1, current.LastExportCommitSequence);
            Assert.Equal("complete-sequence-1", File.ReadAllText(newPath));
        }
        finally
        {
            await original.StopAsync(1000); original.Dispose();
            if (replacement != null) { await replacement.StopAsync(1000); replacement.Dispose(); }
            if (File.Exists(failedPath)) File.Delete(failedPath);
            if (File.Exists(newPath)) File.Delete(newPath);
        }
    }

    [Fact]
    public async Task AbandonedActualExportKeepsPathUntilItsLatePartialFailureIsKnown()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opc-late-partial-{Guid.NewGuid():N}.csv");
        var (first, firstReady) = AcquisitionBudgetTests.CreateOneRecord();
        var (second, secondReady) = AcquisitionBudgetTests.CreateOneRecord();
        var entered = AcquisitionBudgetTests.Signal(); var release = AcquisitionBudgetTests.Signal();
        int writes = 0; Task<ExecutionResult>? retry = null;
        first.CsvExporter = async (file, _, _) =>
        {
            Interlocked.Increment(ref writes); File.AppendAllText(file, "partial"); entered.SetResult();
            await release.Task; throw new IOException("late partial export failure");
        };
        second.CsvExporter = (_, _, _) => { Interlocked.Increment(ref writes); return Task.CompletedTask; };
        try
        {
            await Task.WhenAll(firstReady, secondReady).WaitAsync(TimeSpan.FromSeconds(3));
            var waiting = Read(first, path, 40);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var abandoned = await waiting;
            Assert.Equal(TestStatus.Error, abandoned.StepResult.Status);
            Assert.Equal(1, first.SampleCount);
            retry = Read(second, path);
            await Task.Delay(30); Assert.False(retry.IsCompleted); Assert.Equal(1, writes);
            release.SetResult(); var blocked = await retry;
            Assert.Equal(TestStatus.Error, blocked.StepResult.Status);
            Assert.Contains("写入结果未知", blocked.StepResult.Error!.Message);
            Assert.Equal(1, writes); Assert.Equal(1, first.SampleCount); Assert.Equal(1, second.SampleCount);
            Assert.Equal("partial", File.ReadAllText(path));
        }
        finally
        {
            release.TrySetResult(); if (retry != null) await retry;
            await first.StopAsync(1000); first.Dispose(); await second.StopAsync(1000); second.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<ExecutionResult> Read(OpcUaDataAcqTask task, string path, int timeoutMs = 1000)
    {
        var plugin = new OpcUaDataAcqReadPlugin();
        var setting = new OpcUaDataAcqReadSetting { TaskName = "\"budget\"", TimeoutMs = timeoutMs,
            SaveToFile = true, CsvFilePath = "@\"" + path.Replace("\"", "\"\"") + "\"" };
        var (context, state) = AcquisitionContext.Create(plugin, setting);
        state.Resources["OpcUaDataAcq_budget"] = task;
        await ExpressionEvaluatorFactory.Default.EvalStringAsync(setting.TaskName, context);
        await ExpressionEvaluatorFactory.Default.EvalStringAsync(setting.CsvFilePath, context);
        return await plugin.CreateExecutor().ExecuteAsync(context);
    }
}
