using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.Flash;
using CAN.Flash.HexParser;
using CAN.Flash.Models;
using CAN.UDS;
using CAN.UDS.Models;
using CAN.UI.Services;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class FollowupFlashAndDtcUiTests
{
    [WpfFact]
    public async Task Blocking_file_preflight_runs_off_the_editor_dispatcher()
    {
        string path = Path.GetTempFileName();
        int editorThread = Environment.CurrentManagedThreadId, preflightThread = 0;
        using var release = new ManualResetEventSlim();
        // A mapped/network drive can block Exists before the first awaited read.
        var watchdog = Task.Run(async () => { await Task.Delay(250); release.Set(); });
        try
        {
            File.WriteAllBytes(path, [0xAB]);
            var result = await FirmwareAnalyzer.AnalyzeAsync(JsonSerializer.Serialize(path), FirmwareFormat.Binary,
                "0", default, (_, _) => new ByteParser(), _ =>
                {
                    preflightThread = Environment.CurrentManagedThreadId;
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test preflight guard");
                    return true;
                });
            Assert.True(result.Success, result.Message);
            Assert.NotEqual(editorThread, preflightThread);
        }
        finally { release.Set(); await watchdog; File.Delete(path); }
    }

    [WpfFact]
    public async Task Refresh_from_analysis_message_cannot_publish_old_segments_or_suggestion()
    {
        var vm = new CanFlashViewModel((_, _, _, _) => Task.FromResult(Success()));
        Attach(vm, "first.bin"); bool refreshed = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (refreshed || args.PropertyName != nameof(vm.AnalysisMessage) || !vm.AnalysisMessage.Contains("old-result")) return;
            refreshed = true; Attach(vm, "next.bin");
        };
        await vm.AnalyzeFirmwareAsync();
        Assert.True(refreshed);
        Assert.Empty(vm.AnalysisSegments);
        Assert.False(vm.HasSuggestion);
        vm.Flush();
    }

    [WpfFact]
    public async Task Unload_cancels_only_the_wait_and_prevents_a_second_analysis_until_the_old_worker_really_exits()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<FirmwareAnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var vm = new CanFlashViewModel((_, _, _, _) =>
        {
            int call = Interlocked.Increment(ref calls); entered.TrySetResult();
            return call == 1 ? released.Task : Task.FromResult(Success());
        });
        Attach(vm, "first.bin"); var waiting = vm.AnalyzeFirmwareAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            vm.SuspendAnalysis(); await waiting.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.False(released.Task.IsCompleted); Assert.True(vm.IsAnalyzing);
            Assert.False(vm.AnalysisCompletion.IsCompleted);
            vm.ResumeAnalysis(); await vm.AnalyzeFirmwareAsync();
            Assert.Equal(1, calls); Assert.False(vm.CanAnalyze);
            released.SetResult(Success()); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(vm.CanAnalyze); Assert.False(vm.HasSuggestion); Assert.Empty(vm.AnalysisSegments);
            await vm.AnalyzeFirmwareAsync(); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, calls); Assert.True(vm.HasSuggestion);
        }
        finally { released.TrySetResult(Success()); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5)); vm.Flush(); }
    }

    [WpfFact]
    public async Task A_blocked_cancellation_callback_does_not_block_unload_or_release_the_worker_ownership_early()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<FirmwareAnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim();
        int calls = 0;
        var vm = new CanFlashViewModel((_, _, _, ct) =>
        {
            Interlocked.Increment(ref calls);
            ct.Register(() => { cancelling.TrySetResult(); if (!releaseCallback.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test cancellation guard"); });
            entered.TrySetResult(); return result.Task;
        });
        Attach(vm, "blocked.bin"); var waiting = vm.AnalyzeFirmwareAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var elapsed = System.Diagnostics.Stopwatch.StartNew(); vm.SuspendAnalysis();
            Assert.True(elapsed.ElapsedMilliseconds < 200, $"Unload blocked for {elapsed.ElapsedMilliseconds} ms");
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await waiting.WaitAsync(TimeSpan.FromMilliseconds(500));
            result.SetResult(Success()); await Task.Delay(50);
            vm.ResumeAnalysis(); await vm.AnalyzeFirmwareAsync();
            Assert.Equal(1, calls); Assert.True(vm.IsAnalyzing); Assert.False(vm.AnalysisCompletion.IsCompleted);
            releaseCallback.Set(); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(vm.CanAnalyze); Assert.False(vm.HasSuggestion);
        }
        finally { releaseCallback.Set(); result.TrySetResult(Success()); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5)); vm.Flush(); }
    }

    [WpfTheory]
    [InlineData("unload")]
    [InlineData("refresh")]
    public async Task A_collection_notification_cannot_publish_remaining_segments_into_an_unloaded_or_new_step(string transition)
    {
        var vm = new CanFlashViewModel((_, _, _, _) => Task.FromResult(new FirmwareAnalysisResult
        { Success = true, Message = "complete", SegmentDetails = ["one", "two", "three"], SuggestedAddressAndLengthFormatId = "0x44" }));
        Attach(vm, "first.bin"); bool changed = false;
        vm.AnalysisSegments.CollectionChanged += (_, _) => { }; // A second listener exercises ObservableCollection's reentrancy contract.
        vm.AnalysisSegments.CollectionChanged += (_, args) =>
        {
            if (changed || args.NewItems == null) return;
            changed = true;
            if (transition == "unload") vm.SuspendAnalysis(); else Attach(vm, "next.bin");
        };
        await vm.AnalyzeFirmwareAsync(); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => { });
        Assert.True(changed); Assert.False(vm.HasSuggestion);
        if (transition == "unload") Assert.Single(vm.AnalysisSegments); else Assert.Empty(vm.AnalysisSegments);
        vm.Flush();
    }

    [WpfFact]
    public async Task A_reentrant_step_change_before_worker_submission_never_analyzes_the_new_steps_file()
    {
        int calls = 0;
        var vm = new CanFlashViewModel((_, _, _, _) => { Interlocked.Increment(ref calls); return Task.FromResult(Success()); });
        Attach(vm, "first.bin"); bool changed = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (changed || args.PropertyName != nameof(vm.IsAnalyzing) || !vm.IsAnalyzing) return;
            changed = true; Attach(vm, "next.bin");
        };
        await vm.AnalyzeFirmwareAsync(); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(changed); Assert.Equal(0, calls); Assert.False(vm.HasSuggestion); Assert.True(vm.CanAnalyze);
        vm.Flush();
    }

    [WpfFact]
    public async Task A_failed_analyzer_is_a_visible_message_and_releases_busy_after_actual_exit()
    {
        var vm = new CanFlashViewModel((_, _, _, _) => throw new IOException("fake file failure"));
        Attach(vm, "failed.bin");
        await vm.AnalyzeFirmwareAsync(); await vm.AnalysisCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("fake file failure", vm.AnalysisMessage); Assert.False(vm.HasSuggestion); Assert.True(vm.CanAnalyze);
        vm.Flush();
    }

    [Fact]
    public async Task Precancelled_file_analysis_never_enters_file_preflight()
    {
        int calls = 0; using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var result = await FirmwareAnalyzer.AnalyzeAsync("\"never-open.bin\"", FirmwareFormat.Binary, "0", cancelled.Token,
            (_, _) => new ByteParser(), _ => { Interlocked.Increment(ref calls); return true; });
        Assert.False(result.Success); Assert.Contains("取消", result.Message); Assert.Equal(0, calls);
    }

    [WpfTheory]
    [InlineData(true, -1)] [InlineData(true, 256)] [InlineData(true, 258)]
    [InlineData(false, -1)] [InlineData(false, 256)] [InlineData(false, 258)]
    public void Dtc_integer_input_rejects_out_of_byte_range_without_changing_saved_value(bool subFunction, int value)
    {
        var vm = new UdsReadDtcViewModel(); var plugin = new UdsReadDtcPlugin();
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new UdsReadDtcSetting { SubFunction = 2, StatusMask = 1 });
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => { if (subFunction) vm.SubFunction = value; else vm.StatusMask = value; });
        Assert.Contains("0", error.Message); Assert.Contains("255", error.Message);
        vm.Flush();
        var saved = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
        Assert.Equal((byte)2, saved.SubFunction); Assert.Equal((byte)1, saved.StatusMask);
    }

    [WpfTheory]
    [InlineData(0)] [InlineData(255)]
    public void Dtc_byte_boundaries_save_without_changing_the_wire_type(int value)
    {
        var vm = new UdsReadDtcViewModel(); var plugin = new UdsReadDtcPlugin(); var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new UdsReadDtcSetting());
        vm.AttachSerializer(serializer); vm.AttachStep(step); vm.SubFunction = value; vm.StatusMask = value; vm.Flush();
        var saved = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
        Assert.Equal((byte)value, saved.SubFunction); Assert.Equal((byte)value, saved.StatusMask);
    }

    // Called from the process-wide shared STA, never a second Syncfusion dispatcher.
    internal static void VerifyActualPages()
    {
        var failures = new List<string>();
        var plugin = new UdsReadDtcPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new UdsReadDtcSetting { SubFunction = 2, StatusMask = 1 });
        var view = new UdsReadDtcEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(900, 850)); view.Arrange(new Rect(0, 0, 900, 850)); view.UpdateLayout();
        foreach (string property in new[] { "SubFunction", "StatusMask" })
        {
            var input = Logical(view).OfType<TextBox>().Single(text => BindingOperations.GetBinding(text, TextBox.TextProperty)?.Path.Path == property);
            foreach (int invalid in new[] { -1, 256, 258 })
            {
                input.Text = invalid.ToString(); input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                if (!Validation.GetHasError(input)) failures.Add($"{property}={invalid} 没有显示校验错误");
                view.ViewModel.Flush();
                var saved = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
                byte actual = property == "SubFunction" ? saved.SubFunction : saved.StatusMask;
                if (actual != (property == "SubFunction" ? 2 : 1)) failures.Add($"{property}={invalid} 被截断并写入 {actual}");
            }
            foreach (int valid in new[] { 0, 255 })
            {
                input.Text = valid.ToString(); input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                if (Validation.GetHasError(input)) failures.Add($"{property}={valid} 被错误拒绝");
                view.ViewModel.Flush();
                var saved = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
                if ((property == "SubFunction" ? saved.SubFunction : saved.StatusMask) != valid) failures.Add($"{property}={valid} 未保存");
            }
        }
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var flash = new CanFlashEditorView(new CanFlashViewModel((_, _, _, _) => Task.FromResult(Success())));
        flash.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        if (flash.ViewModel.CanAnalyze) failures.Add("Flash 页面卸载后仍允许发起分析");
        flash.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        if (!flash.ViewModel.CanAnalyze) failures.Add("Flash 页面重新加载后不能分析");
        flash.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static FirmwareAnalysisResult Success() => new() { Success = true, Message = "old-result", SegmentDetails = ["old-segment"], SuggestedAddressAndLengthFormatId = "0x44" };
    private static Step Attach(CanFlashViewModel vm, string path)
    {
        var serializer = new CanFlashPlugin().CreateSerializer(); var step = new Step();
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new CanFlashSetting { FilePath = JsonSerializer.Serialize(path) });
        vm.AttachSerializer(serializer); vm.AttachStep(step); return step;
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var nested in Logical(child)) yield return nested;
    }
    private sealed class ByteParser : IFirmwareParser
    { public IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress) => [new FlashSegment { StartAddress = baseAddress, Data = content }]; }
}
