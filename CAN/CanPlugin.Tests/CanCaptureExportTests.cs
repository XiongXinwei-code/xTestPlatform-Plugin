using System.IO;
using System.Reflection;
using System.Text.Json;
using CAN.Models;
using CAN.Runtime;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanCaptureExportTests
{
    [Fact]
    public async Task Enabled_export_writes_the_same_json_as_result_variable()
    {
        var folder = Path.Combine(Path.GetTempPath(), "can-capture-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "录制.json");
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>();
        var state = (CaptureContext)(object)context;
        var owner = new CanConnectionResource(new FakeAdapter()); owner.Open(new());
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "original";
        var setting = new CanCaptureSetting { DurationMs = 30, ResultVariable = "Locals.Captured" };
        state.Setting = setting;
        try
        {
            setting.ExportEnabled = true;
            setting.ExportFilePath = JsonSerializer.Serialize(path);
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            Assert.Equal(state.Variables["Locals.Captured"], await File.ReadAllTextAsync(path));
            Assert.False((await File.ReadAllBytesAsync(path)).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally
        {
            await owner.CloseAsync();
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void Legacy_settings_leave_export_disabled()
    {
        var setting = (CanCaptureSetting)new CanCapturePlugin().CreateSerializer().Deserialize([0x80], 1);
        Assert.False(setting.ExportEnabled);
        Assert.False(setting.OverwriteExportFile);
        Assert.Equal("\"\"", setting.ExportFilePath);
        Assert.Equal(5000, setting.ExportTimeoutMs);
    }

    [Fact]
    public async Task Disabled_export_does_not_evaluate_unused_path_or_timeout()
    {
        await using var run = new CaptureRun();
        run.Setting.ExportEnabled = false; run.Setting.ExportFilePath = "ThisIsNotAnExpression(";
        run.Setting.ExportTimeoutMs = 0;
        Assert.Equal(TestStatus.Passed, (await run.Execute()).StepResult.Status);
        Assert.Empty(Directory.GetFiles(run.Folder));
    }

    [Fact]
    public async Task Existing_file_is_preserved_by_default_and_can_be_explicitly_overwritten()
    {
        await using var run = new CaptureRun();
        await File.WriteAllTextAsync(run.Path, "old recording");
        var failed = await run.Execute();
        Assert.Equal(TestStatus.Error, failed.StepResult.Status);
        Assert.Equal("old recording", await File.ReadAllTextAsync(run.Path));
        Assert.Equal("original", run.State.Variables["Locals.Captured"]);
        Assert.Single(Directory.GetFiles(run.Folder));
        run.Setting.OverwriteExportFile = true;
        Assert.Equal(TestStatus.Passed, (await run.Execute()).StepResult.Status);
        Assert.Equal(run.State.Variables["Locals.Captured"], await File.ReadAllTextAsync(run.Path));
    }

    [Fact]
    public async Task Missing_directory_is_error_and_does_not_publish_result()
    {
        await using var run = new CaptureRun();
        run.Setting.ExportFilePath = JsonSerializer.Serialize(System.IO.Path.Combine(run.Folder, "missing", "capture.json"));
        Assert.Equal(TestStatus.Error, (await run.Execute()).StepResult.Status);
        Assert.Equal("original", run.State.Variables["Locals.Captured"]);
        Assert.Empty(Directory.GetFiles(run.Folder));
    }

    [Fact]
    public async Task Variable_failure_reports_that_file_has_already_been_exported()
    {
        await using var run = new CaptureRun();
        run.State.FailCapturePublication = true;
        var result = await run.Execute();
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("文件已导出", result.StepResult.Error!.Message);
        Assert.Contains(run.Path, result.StepResult.Error.Message);
        Assert.True(File.Exists(run.Path));
        Assert.Equal("original", run.State.Variables["Locals.Captured"]);
    }

    [Fact]
    public async Task Publish_failure_and_temporary_cleanup_failure_are_both_reported()
    {
        await using var run = new CaptureRun();
        var error = await Assert.ThrowsAsync<AggregateException>(() => new CanCaptureFileExport(new FailingIo())
            .ExportAsync(run.Path, "new", false, 5000, default, () => { }));
        Assert.Contains("publish failure", error.ToString());
        Assert.Contains("cleanup failure", error.ToString());
        Assert.False(File.Exists(run.Path));
        Assert.Single(Directory.GetFiles(run.Folder));
        // Cleanup failed, but no file call remains: a new export is admitted after actual exit.
        await CanCaptureFileExport.Default.ExportAsync(run.Path, "new", false, 5000, default, () => { });
    }

    [Fact]
    public async Task Cancelled_capture_exports_nothing_and_same_path_can_be_reused()
    {
        await using var run = new CaptureRun();
        run.Setting.DurationMs = 10000;
        using var cancel = new CancellationTokenSource();
        var actual = run.Execute(cancel.Token);
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run.Owner)!;
        using var limit = new CancellationTokenSource(5000);
        while (router.CaptureCount == 0) await Task.Delay(1, limit.Token);
        cancel.Cancel();
        Assert.Equal(TestStatus.Aborted, (await actual).StepResult.Status);
        while (router.CaptureCount != 0) await Task.Delay(1, limit.Token);
        Assert.False(File.Exists(run.Path));
        Assert.Equal("original", run.State.Variables["Locals.Captured"]);
        run.Setting.DurationMs = 30;
        Assert.Equal(TestStatus.Passed, (await run.Execute()).StepResult.Status);
        Assert.True(File.Exists(run.Path));
    }

    [Theory]
    [InlineData("relative.json")]
    [InlineData("C:\\capture.json:stream")]
    [InlineData("\\\\.\\NUL")]
    [InlineData("C:\\recordings\\file. ")]
    [InlineData("C:\\recordings\\")]
    [InlineData("C:\\NUL.json")]
    [InlineData("//./NUL")]
    public void Rejects_ambiguous_or_non_file_export_paths(string path)
        => Assert.Throws<ArgumentException>(() => CanCaptureFileExport.NormalizePath(path));

    [Fact]
    public async Task Editor_only_requires_export_fields_when_enabled()
    {
        await using var run = new CaptureRun();
        var setting = run.Setting;
        setting.ExportFilePath = "\"\""; setting.ExportTimeoutMs = 0;
        async Task<IReadOnlyList<xTestPlatform.Core.Plugins.Contracts.StepSettingError>> Validate()
        {
            var serializer = new CanCapturePlugin().CreateSerializer(); var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = 1;
            return await new CAN.UI.CanCaptureEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
            { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new SequenceFile(),
                ExecutionContext = run.Context, Evaluator = ExpressionEvaluatorFactory.Default });
        }
        var errors = await Validate();
        Assert.Contains(errors, e => e.Code == "CAN_CAPTURE_EXPORT_PATH");
        Assert.Contains(errors, e => e.Code == "CAN_CAPTURE_EXPORT_TIMEOUT");
        setting.ExportEnabled = false;
        Assert.DoesNotContain(await Validate(), e => e.Code.StartsWith("CAN_CAPTURE_EXPORT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_or_cancel_keeps_path_owned_until_real_worker_exits(bool cancelCaller)
    {
        await using var run = new CaptureRun();
        var io = new BlockingCreateIo();
        var exporter = new CanCaptureFileExport(io);
        using var caller = new CancellationTokenSource();
        var actual = exporter.ExportAsync(run.Path, "old", true, cancelCaller ? 5000 : 200, caller.Token, () => { });
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancelCaller) caller.Cancel();
            if (cancelCaller) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actual);
            else await Assert.ThrowsAsync<TimeoutException>(() => actual);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CanCaptureFileExport.Default.ExportAsync(run.Path, "new", true, 5000, default, () => { }));
            io.Release.Set();
            using var limit = new CancellationTokenSource(5000);
            while (true)
            {
                limit.Token.ThrowIfCancellationRequested();
                try { await CanCaptureFileExport.Default.ExportAsync(run.Path, "new", true, 5000, default, () => { }); break; }
                catch (InvalidOperationException) { await Task.Delay(5, limit.Token); }
            }
            Assert.Equal(0, io.Moves);
            Assert.Equal("new", await File.ReadAllTextAsync(run.Path));
            Assert.Single(Directory.GetFiles(run.Folder));
        }
        finally { io.Release.Set(); }
    }

    [Fact]
    public async Task Retired_execution_before_commit_preserves_existing_file()
    {
        await using var run = new CaptureRun();
        await File.WriteAllTextAsync(run.Path, "old");
        int checks = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CanCaptureFileExport.Default.ExportAsync(run.Path,
            "new", true, 5000, default, () => { if (++checks == 4) throw new InvalidOperationException("retired"); }));
        Assert.Equal("old", await File.ReadAllTextAsync(run.Path));
        Assert.Single(Directory.GetFiles(run.Folder));
    }

    [Fact]
    public async Task In_progress_rename_retains_path_until_it_returns_even_after_timeout()
    {
        await using var run = new CaptureRun();
        var io = new BlockingCreateIo(blockMove: true);
        var export = new CanCaptureFileExport(io).ExportAsync(run.Path, "old", true, 200, default, () => { });
        try
        {
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => export);
            Assert.Contains("已准入文件发布", error.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CanCaptureFileExport.Default.ExportAsync(run.Path, "new", true, 5000, default, () => { }));
            io.Release.Set();
            using var limit = new CancellationTokenSource(5000);
            while (true)
            {
                limit.Token.ThrowIfCancellationRequested();
                try { await CanCaptureFileExport.Default.ExportAsync(run.Path, "new", true, 5000, default, () => { }); break; }
                catch (InvalidOperationException) { await Task.Delay(5, limit.Token); }
            }
            Assert.Equal(1, io.Moves);
            Assert.Equal("new", await File.ReadAllTextAsync(run.Path));
            Assert.Single(Directory.GetFiles(run.Folder));
        }
        finally { io.Release.Set(); }
    }

    private sealed class BlockingCreateIo(bool blockMove = false) : ICanCaptureFileIo
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();
        internal int Moves;
        public Stream CreateNew(string path)
        {
            if (!blockMove) Block();
            return new CanCaptureFileIo().CreateNew(path);
        }
        private void Block()
        {
            Entered.TrySetResult();
            if (!Release.Wait(10000)) throw new TimeoutException("test did not release file I/O");
        }
        public void Move(string source, string destination, bool overwrite)
        {
            if (blockMove) Block();
            Interlocked.Increment(ref Moves); File.Move(source, destination, overwrite);
        }
        public void Delete(string path) => File.Delete(path);
    }

    private sealed class FailingIo : ICanCaptureFileIo
    {
        public Stream CreateNew(string path) => new CanCaptureFileIo().CreateNew(path);
        public void Move(string source, string destination, bool overwrite) => throw new IOException("publish failure");
        public void Delete(string path) => throw new IOException("cleanup failure");
    }

    private sealed class CaptureRun : IAsyncDisposable
    {
        internal readonly string Folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "can-capture-test-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Folder, "capture.json");
        internal readonly IExecutionContext Context = DispatchProxy.Create<IExecutionContext, CaptureContext>();
        internal CaptureContext State => (CaptureContext)(object)Context;
        internal readonly CanConnectionResource Owner = new(new FakeAdapter());
        internal readonly CanCaptureSetting Setting = new() { DurationMs = 30, ResultVariable = "Locals.Captured", ExportEnabled = true };
        internal CaptureRun()
        {
            Directory.CreateDirectory(Folder); Owner.Open(new());
            State.Adapter = Owner; State.Plugin = new CanCapturePlugin(); State.Setting = Setting;
            State.Variables["Locals.Captured"] = "original"; Setting.ExportFilePath = JsonSerializer.Serialize(Path);
        }
        internal Task<ExecutionResult> Execute(CancellationToken token = default)
            => State.Plugin.CreateExecutor().ExecuteAsync(Context, token);
        public async ValueTask DisposeAsync()
        {
            await Owner.CloseAsync();
            foreach (var file in Directory.GetFiles(Folder)) File.Delete(file);
            Directory.Delete(Folder);
        }
    }
}
