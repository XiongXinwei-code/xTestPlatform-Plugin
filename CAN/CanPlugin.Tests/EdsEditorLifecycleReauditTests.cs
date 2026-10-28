using System.IO;
using System.Windows;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.UI.Services;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class EdsEditorLifecycleReauditTests
{
    [WpfFact]
    public async Task File_read_does_not_run_on_the_editor_dispatcher()
    {
        string path = Path.GetTempFileName();
        var vm = new ControlledEdsViewModel();
        int dispatcherThread = Environment.CurrentManagedThreadId;
        try
        {
            Attach(vm);
            vm.EdsFilePath = path;
            await vm.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotEqual(dispatcherThread, vm.ReadThread);
        }
        finally
        {
            vm.Release.Set(); await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            vm.Flush(); File.Delete(path);
        }
    }

    [WpfTheory]
    [InlineData("path")]
    [InlineData("step")]
    [InlineData("unload")]
    [InlineData("reenter")]
    public async Task Old_file_read_cannot_publish_after_path_step_or_editor_lifecycle_changes(string change)
    {
        var vm = new ControlledEdsViewModel { BlockUntilReleased = true };
        try
        {
            Attach(vm); vm.EdsFilePath = "old.eds";
            await vm.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (change == "path") { vm.EdsFilePath = "superseded.eds"; vm.EdsFilePath = "latest.eds"; }
            else if (change == "step") Attach(vm, "latest.eds");
            else
            {
                vm.SuspendEdsLoading();
                if (change == "reenter") vm.ResumeEdsLoading();
            }
            Assert.Equal(1, vm.ReadCount); // No second OS read before the first one actually exits.
            Assert.Empty(vm.EdsObjects);
            vm.Release.Set();
            await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, vm.MaxConcurrentReads);
            if (change == "unload")
            {
                Assert.Empty(vm.EdsObjects); Assert.Equal(1, vm.ReadCount);
            }
            else
            {
                Assert.Equal(2, vm.ReadCount);
                var entry = Assert.Single(vm.EdsObjects);
                Assert.Equal(change == "reenter" ? "old.eds" : "latest.eds", entry.Name);
                Assert.Equal((ushort)0x2002, entry.Index); // The cancelled first result is never reused.
                Assert.DoesNotContain("superseded.eds", vm.Paths);
            }
        }
        finally { vm.Release.Set(); await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5)); vm.Flush(); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_failure_is_visible_and_leaves_manual_fields_unchanged(bool missing)
    {
        var vm = new ControlledEdsViewModel
        {
            Failure = missing ? new FileNotFoundException() : new InvalidDataException("fake malformed EDS")
        };
        Attach(vm);
        vm.Index = "0x6041"; vm.SubIndex = "0";
        vm.EdsFilePath = "failed.eds";
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(vm.EdsObjects);
        Assert.Contains(missing ? "文件不存在" : "解析失败", vm.EdsStatus);
        Assert.Equal("0x6041", vm.Index); Assert.Equal("0", vm.SubIndex);
        vm.Flush();
    }

    [Fact]
    public void Parser_accepts_complete_EDS_and_observes_pre_cancelled_reads()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "[6041]\nParameterName=状态字\nDataType=0x0006\nAccessType=ro\n");
            Assert.Equal("状态字", Assert.Single(EdsParser.Parse(path)).Name);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => EdsParser.Parse(path, cancelled.Token));
        }
        finally { File.Delete(path); }
    }

    [WpfFact]
    public async Task Unload_during_a_collection_notification_stops_further_old_publication()
    {
        var vm = new ControlledEdsViewModel { EntryCount = 3 };
        Attach(vm);
        vm.EdsObjects.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null) vm.SuspendEdsLoading();
        };
        vm.EdsFilePath = "unload-during-publish.eds";
        vm.Release.Set();
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(vm.EdsObjects); // The first item caused Unloaded; remaining items must not be published.
        Assert.DoesNotContain("已加载", vm.EdsStatus);
        vm.Flush();
    }

    [WpfTheory]
    [InlineData("clear")]
    [InlineData("selection")]
    public async Task Initialization_notification_cannot_overwrite_a_reentrant_newer_file_request(string notification)
    {
        var vm = new ControlledEdsViewModel();
        Attach(vm); bool changed = false;
        void ChangeOnce()
        {
            if (changed) return;
            changed = true; vm.EdsFilePath = "latest.eds";
        }
        if (notification == "clear") vm.EdsObjects.CollectionChanged += (_, args) =>
        { if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) ChangeOnce(); };
        else vm.PropertyChanged += (_, args) =>
        { if (args.PropertyName == nameof(vm.SelectedEdsObject)) ChangeOnce(); };
        vm.EdsFilePath = "first.eds"; vm.Release.Set();
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(changed);
        Assert.Equal("latest.eds", Assert.Single(vm.EdsObjects).Name);
        Assert.Contains("已加载", vm.EdsStatus);
        vm.Flush();
    }

    [WpfFact]
    public async Task Refresh_during_publication_with_multiple_collection_observers_loads_the_new_step()
    {
        var vm = new ControlledEdsViewModel { EntryCount = 3 };
        Attach(vm); bool refreshed = false;
        vm.EdsObjects.CollectionChanged += (_, _) => { }; // e.g. the actual WPF CollectionView.
        vm.EdsObjects.CollectionChanged += (_, args) =>
        {
            if (refreshed || args.NewItems == null) return;
            refreshed = true; Attach(vm, "new-step.eds");
        };
        vm.EdsFilePath = "old-step.eds"; vm.Release.Set();
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(refreshed);
        Assert.Equal(3, vm.EdsObjects.Count);
        Assert.All(vm.EdsObjects, entry => Assert.Equal("new-step.eds", entry.Name));
        Assert.Contains("已加载", vm.EdsStatus); vm.Flush();
    }

    [WpfFact]
    public async Task A_cached_old_selection_cannot_write_fields_after_a_path_change()
    {
        var vm = new ControlledEdsViewModel(); Attach(vm); vm.Release.Set();
        vm.EdsFilePath = "first.eds";
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        var oldEntry = Assert.Single(vm.EdsObjects);
        vm.Index = "0x6041";
        vm.EdsFilePath = "second.eds";
        vm.SelectedEdsObject = oldEntry;
        Assert.Equal("0x6041", vm.Index);
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("second.eds", Assert.Single(vm.EdsObjects).Name); vm.Flush();
    }

    [WpfFact]
    public async Task A_selection_notification_refresh_cannot_fill_the_new_steps_fields()
    {
        var vm = new ControlledEdsViewModel(); Attach(vm); vm.Release.Set();
        vm.EdsFilePath = "first.eds";
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        var oldEntry = Assert.Single(vm.EdsObjects); bool refreshed = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (refreshed || args.PropertyName != nameof(vm.SelectedEdsObject)) return;
            refreshed = true; Attach(vm, "new-step.eds");
        };
        vm.SelectedEdsObject = oldEntry;
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(refreshed);
        Assert.Equal(new CanopenSdoReadSetting().Index, vm.Index);
        Assert.Equal(new CanopenSdoReadSetting().SubIndex, vm.SubIndex); vm.Flush();
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_reentrant_selection_in_the_same_file_keeps_the_new_selection_fields(bool clear)
    {
        var vm = new ControlledEdsViewModel { EntryCount = 2 }; Attach(vm); vm.Release.Set();
        vm.EdsFilePath = "two-objects.eds";
        await vm.EdsLoadCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        var first = vm.EdsObjects[0]; var second = vm.EdsObjects[1];
        vm.Index = "0x6041"; bool selected = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (selected || args.PropertyName != nameof(vm.SelectedEdsObject)) return;
            selected = true; vm.SelectedEdsObject = clear ? null : second;
        };
        vm.SelectedEdsObject = first;
        Assert.True(selected);
        Assert.Same(clear ? null : second, vm.SelectedEdsObject);
        Assert.Equal(clear ? "0x6041" : $"0x{second.Index:X4}", vm.Index);
        if (!clear)
        {
            Assert.Equal(second.SubIndex.ToString(), vm.SubIndex);
            Assert.Equal((int)second.DataType, vm.DataType);
        }
        vm.Flush();
    }

    // Called only from EditorRuntimeTests' shared STA to avoid process-wide
    // Syncfusion/WPF caches crossing Dispatchers during the complete suite.
    internal static void VerifyActualSdoPageReentry()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "[6041]\nDataType=0x0006\nParameterName=status\n");
            foreach (bool write in new[] { false, true })
            {
                var step = new Step();
                var plugin = write ? (xTestPlatform.Core.Plugins.Contracts.IStepPlugin)new CanopenSdoWritePlugin() : new CanopenSdoReadPlugin();
                step.StepSetting.Setting = plugin.CreateSerializer().Serialize(plugin.CreateSerializer().CreateDefault());
                step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
                IStepEditorPlugin editor = write ? new CAN.UI.CanopenSdoWriteEditorPlugin() : new CAN.UI.CanopenSdoReadEditorPlugin();
                var page = editor.CreateEditor(step, new SequenceFile());
                if (page is CanopenSdoReadEditorView read)
                {
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    read.ViewModel.EdsFilePath = path;
                    Assert.Empty(read.ViewModel.EdsStatus); Assert.Empty(read.ViewModel.EdsObjects);
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Assert.False(read.ViewModel.EdsLoadCompletion.IsCompleted);
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    Assert.Equal(path, ((CanopenSdoReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).EdsFilePath);
                }
                else
                {
                    var view = Assert.IsType<CanopenSdoWriteEditorView>(page);
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    view.ViewModel.EdsFilePath = path;
                    Assert.Empty(view.ViewModel.EdsStatus); Assert.Empty(view.ViewModel.EdsObjects);
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Assert.False(view.ViewModel.EdsLoadCompletion.IsCompleted);
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    Assert.Equal(path, ((CanopenSdoWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).EdsFilePath);
                }
            }
        }
        finally { File.Delete(path); }
    }

    private static Step Attach(ControlledEdsViewModel vm, string path = "")
    {
        var serializer = new CanopenSdoReadPlugin().CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanopenSdoReadSetting { EdsFilePath = path });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        return step;
    }

    private sealed class ControlledEdsViewModel : CanopenSdoReadViewModel
    {
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int ReadThread;
        internal bool BlockUntilReleased;
        internal Exception? Failure;
        internal int ReadCount;
        internal int MaxConcurrentReads;
        internal int EntryCount = 1;
        internal readonly List<string> Paths = new();
        private int _concurrent;

        protected override IReadOnlyList<EdsObjectEntry> ReadEdsObjects(string path, CancellationToken cancellationToken)
        {
            ReadThread = Environment.CurrentManagedThreadId;
            int call = Interlocked.Increment(ref ReadCount);
            int concurrent = Interlocked.Increment(ref _concurrent);
            MaxConcurrentReads = Math.Max(MaxConcurrentReads, concurrent);
            lock (Paths) Paths.Add(path);
            try
            {
                Entered.TrySetResult();
                // The fake can ignore cancellation just like a blocked OS open.
                // Only the test's release/guard ends that actual read.
                if (call == 1) Release.Wait(BlockUntilReleased ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(300));
                if (Failure != null) throw Failure;
                return Enumerable.Range(0, EntryCount).Select(offset => new EdsObjectEntry
                {
                    Index = (ushort)(0x2000 + call + offset), SubIndex = (byte)offset,
                    DataType = offset == 0 ? CanopenDataType.Unsigned16 : CanopenDataType.Integer32,
                    Name = Path.GetFileName(path)
                }).ToArray();
            }
            finally { Interlocked.Decrement(ref _concurrent); }
        }
    }
}
