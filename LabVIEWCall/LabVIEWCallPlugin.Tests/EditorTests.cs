using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Views;
using StepEditor.Abstractions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using xTestPlatform.Core.SequenceModels;

using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class EditorTests
{
    private const string Input = """[[["number"],{"Name":"number","Type":"I32","Value":"42"}]]""";

    private static Step MakeStep(string path, string input = "", int timeout = 5000, int port = 3363)
    {
        var serializer = new LabVIEWCallStepPlugin().CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = new LabVIEWCallStepPlugin().StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new LabVIEWCallSetting
            { ViFilePath = path, InputParameters = input, TimeoutMs = timeout, ViServerPort = port });
        return step;
    }

    private static LabVIEWCallSetting Read(Step step) => (LabVIEWCallSetting)new LabVIEWCallStepPlugin()
        .CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

    [WpfFact]
    public async Task Page_flushes_original_step_and_late_panel_cannot_overwrite_next()
    {
        var path = Path.GetTempFileName();
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var unblock = new ManualResetEventSlim();
        var first = MakeStep(path);
        var next = MakeStep("B.vi", Input);
        var view = new LabVIEWCallEditorView(first, null, (_, _, _, _, _) =>
        {
            called.TrySetResult();
            try { unblock.Wait(TimeSpan.FromSeconds(10)); }
            finally { released.TrySetResult(); }
            return new ConnectPanelInfo { VIState = VIState.Idle, ControlOut = "[]", IndicatorOut = "[]" };
        });
        var window = new Window { Content = view, Width = 900, Height = 500,
            Left = -32000, Top = -32000, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show();
            window.UpdateLayout();
            view.Setting!.TimeoutMs = 1234;
            var oldLoad = view.LoadPanelAsync(true);
            await called.Task.WaitAsync(TimeSpan.FromSeconds(3));
            view.RefreshFromStep(next);
            Assert.Equal(1234, Read(first).TimeoutMs);
            unblock.Set();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await oldLoad;
            var panel = (LvPanelView)view.FindName("LvPanel");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (panel.ViewModel.IsLoading)
                await Task.Delay(10, deadline.Token);
            Assert.Equal("B.vi", view.Setting!.ViFilePath);
            Assert.Equal("number", Assert.Single(panel.ViewModel.ControlNodes).Name);
            Assert.Equal(Input, Read(next).InputParameters);

            view.Setting.TimeoutMs = 4321;
            window.Close();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(4321, Read(next).TimeoutMs);
            // Syncfusion licenses and controls have dispatcher affinity; drive all page scenarios on this UI thread.
            await Failed_online_panel_read_preserves_saved_mapping();
            await Missing_node_menu_removes_target_and_updates_only_its_binding();
            await Stalled_file_lookup_does_not_block_page_or_open_switched_vi(false);
            await Stalled_file_lookup_does_not_block_page_or_open_switched_vi(true);
            await Malformed_online_documents_preserve_the_entire_saved_step();
            await Editing_the_same_step_during_load_prevents_late_candidate_publication();
            await Port_change_cancels_old_online_request_and_discards_its_result(false);
            await Port_change_cancels_old_online_request_and_discards_its_result(true);
            await Port_saves_through_host_command_and_layout_remains_accessible();
        }
        finally { unblock.Set(); if (window.IsVisible) window.Close(); File.Delete(path); }
    }

    private static async Task Port_change_cancels_old_online_request_and_discards_its_result(bool edit)
    {
        var step = MakeStep("original.vi", Input);
        var entered = new TaskCompletionSource<(int Port, CancellationToken Token)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        ConnectPanelInfo ReadPanel(string path, string inputs, string outputs, CancellationToken token, int port)
        {
            entered.TrySetResult((port, token));
            release.Wait(TimeSpan.FromSeconds(5));
            return new() { VIState = VIState.Idle, ControlOut = "[]", IndicatorOut = "[]" };
        }
        var view = new LabVIEWCallEditorView(step, null, ReadPanel, _ => true,
            (_, token, port) => { ReadPanel("", "", "", token, port); });
        view.RefreshFromStep(step);
        await view.LoadPanelAsync(false);
        var pending = edit ? view.EditViAsync() : view.LoadPanelAsync(true, "candidate.vi");
        try
        {
            var admitted = await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(3363, admitted.Port);
            view.Setting!.ViServerPort = 4455;
            Assert.True(admitted.Token.IsCancellationRequested);
            await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("original.vi", view.Setting.ViFilePath);
            Assert.Equal(Input, view.Setting.InputParameters);
            var panel = (LvPanelView)view.FindName("LvPanel");
            Assert.False(panel.ViewModel.IsLoading);
            Assert.Equal(UI.Models.LabViewIdeState.Unknown, panel.ViewModel.LabViewIdeState);
        }
        finally { release.Set(); }
        await ViCallGate.SharedIdle.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(4455, view.Setting!.ViServerPort);
        Assert.Equal(Input, view.Setting.InputParameters);
    }

    private static async Task Port_saves_through_host_command_and_layout_remains_accessible()
    {
        var step = MakeStep(@"C:\fixture.vi", Input);
        var view = new LabVIEWCallEditorView(step, null, (_, _, _, _, _) => throw new InvalidOperationException("模拟在线失败"), _ => true);
        int saves = 0;
        string description = "";
        view.ExecuteCommand = (_, apply) =>
        {
            apply();
            description = new LabVIEWCallStepPlugin().GenerateDescription(step.StepSetting.Setting);
            saves++;
        };
        var window = new Window { Content = view, Width = 1500, Height = 850, WindowStyle = WindowStyle.None,
            Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show();
            await view.LoadPanelAsync(false);
            window.UpdateLayout();
            var port = (TextBox)view.FindName("ViServerPortTextBox");
            Assert.Equal("3363", port.Text);
            port.Text = "4455";
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                while (saves == 0) await Task.Delay(20, deadline.Token);
            Assert.Equal(4455, Read(step).ViServerPort);
            Assert.Contains("127.0.0.1:4455", description);

            var panel = (LvPanelView)view.FindName("LvPanel");
            var preview = (Grid)panel.FindName("PreviewPane");
            var status = (TextBlock)panel.FindName("IdeStatusText");
            var splitter = (GridSplitter)panel.FindName("PaneSplitter");
            var layout = (Grid)panel.FindName("PanelLayout");
            Assert.Equal(UI.Models.LabViewIdeState.Unknown, panel.ViewModel.LabViewIdeState);
            Assert.False(panel.ViewModel.IsLabViewDisconnected);
            Assert.Equal("尚未在线检查", panel.ViewModel.ConnectionStatusTitle);
            Assert.Equal(TextWrapping.Wrap, status.TextWrapping);
            Assert.Equal(GridResizeDirection.Columns, splitter.ResizeDirection);
            Assert.Equal(2, Grid.GetColumn(preview));
            Assert.True(status.ActualHeight >= status.DesiredSize.Height - 1);
            SaveRender(view, "wide-1500.png");

            layout.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            layout.ColumnDefinitions[2].Width = new GridLength(2, GridUnitType.Star);
            window.Width = 600;
            window.UpdateLayout();
            Assert.Equal(GridResizeDirection.Rows, splitter.ResizeDirection);
            Assert.Equal(2, Grid.GetRow(preview));
            Assert.Equal(0, Grid.GetColumn(preview));
            Assert.True(preview.ActualWidth <= panel.ActualWidth);
            Assert.True(status.ActualHeight >= status.DesiredSize.Height - 1);
            SaveRender(view, "narrow-600.png");

            await view.LoadPanelAsync(true);
            Assert.True(panel.ViewModel.IsLabViewDisconnected);
            Assert.Equal("在线检查失败", panel.ViewModel.ConnectionStatusTitle);
            Assert.Equal(Input, view.Setting!.InputParameters);
            window.Height = 400;
            window.UpdateLayout();
            var scroll = (ScrollViewer)view.FindName("EditorScroll");
            Assert.True(scroll.ScrollableHeight > 0);
            scroll.ScrollToBottom();
            window.UpdateLayout();
            Assert.True(scroll.VerticalOffset > 0);
            var bottom = preview.TransformToAncestor(view).Transform(new Point(0, preview.ActualHeight));
            Assert.True(bottom.Y <= view.ActualHeight + 1, $"Preview bottom {bottom.Y} exceeds view {view.ActualHeight}");
            SaveRender(view, "narrow-short-scrolled.png");

            window.Width = 1500;
            window.Height = 850;
            window.UpdateLayout();
            Assert.Equal(3, layout.ColumnDefinitions[0].Width.Value);
            Assert.Equal(2, layout.ColumnDefinitions[2].Width.Value);
        }
        finally { window.Close(); }
    }

    private static void SaveRender(FrameworkElement view, string filename)
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "xTestPlatform_StepPlugin_Development_Guide.md")))
            root = Directory.GetParent(root)?.FullName;
        Assert.NotNull(root);
        string directory = Path.Combine(root!, "artifacts", "validation", "labview-port-diagnostics", "ui");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, filename));
        encoder.Save(output);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(65536, false)]
    [InlineData(1, true)]
    [InlineData(3363, true)]
    [InlineData(65535, true)]
    public async Task PortIsSerializedAndValidatedAgainstTheDocumentedRange(int port, bool valid)
    {
        var step = MakeStep(@"C:\fixture.vi", port: port);
        Assert.Equal(port, Read(step).ViServerPort);
        var errors = await new LabVIEWCallEditorPlugin(_ => true).ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = step.StepSetting.Setting, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [],
            Evaluator = null!, ExecutionContext = null!
        });
        Assert.Equal(!valid, errors.Any(error => error.Code == "LV_PORT_INVALID"));
        Assert.Contains("ViServerPort", new LabVIEWCallStepPlugin().Description);
    }

    private static async Task Stalled_file_lookup_does_not_block_page_or_open_switched_vi(bool edit)
    {
        var step = MakeStep("old.vi", Input);
        var next = MakeStep("new.vi", Input);
        int nativeCalls = 0;
        int dispatcherThread = Environment.CurrentManagedThreadId;
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var view = new LabVIEWCallEditorView(step, null,
            (_, _, _, _, _) => { Interlocked.Increment(ref nativeCalls); return new ConnectPanelInfo(); },
            _ =>
            {
                entered.TrySetResult(Environment.CurrentManagedThreadId);
                try { release.Wait(TimeSpan.FromSeconds(6)); return true; }
                finally { finished.TrySetResult(); }
            },
            (_, _, _) => Interlocked.Increment(ref nativeCalls));
        view.RefreshFromStep(step);
        try
        {
            var waiting = edit ? view.EditViAsync() : view.LoadPanelAsync(true);
            Assert.NotEqual(dispatcherThread, await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            view.RefreshFromStep(next);
            await waiting.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("new.vi", view.Setting!.ViFilePath);
        }
        finally
        {
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        Assert.Equal(0, nativeCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StalledValidationPathHonorsCancellationAndConfiguredDeadline(bool cancel)
    {
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var validator = new LabVIEWCallEditorPlugin(_ =>
        {
            entered.TrySetResult(Environment.CurrentManagedThreadId);
            try { release.Wait(TimeSpan.FromSeconds(6)); return false; }
            finally { finished.TrySetResult(); }
        });
        var step = MakeStep(Path.Combine(Path.GetTempPath(), "simulated-network-path.vi"), timeout: cancel ? 0 : 300);
        try
        {
            int callingThread = Environment.CurrentManagedThreadId;
            var waiting = validator.ValidateWithContextAsync(new StepEditorValidationContext
            {
                Setting = step.StepSetting.Setting, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [],
                Evaluator = null!, ExecutionContext = null!
            }, caller.Token);
            Assert.NotEqual(callingThread, await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            if (cancel)
            {
                caller.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(3)));
            }
            else Assert.Contains(await waiting.WaitAsync(TimeSpan.FromSeconds(3)), x => x.Code == "LV_PATH_TIMEOUT");
        }
        finally
        {
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private static async Task Failed_online_panel_read_preserves_saved_mapping()
    {
        string path = Path.GetTempFileName();
        var step = MakeStep("original.vi", Input);
        try
        {
            var view = new LabVIEWCallEditorView(step, null, (candidate, inputs, outputs, _, _) =>
            {
                Assert.Equal(path, candidate);
                Assert.Equal("", inputs);
                Assert.Equal("", outputs);
                return new ConnectPanelInfo { VIState = VIState.Bad };
            });
            view.RefreshFromStep(step);
            await view.LoadPanelAsync(true, path);
            Assert.Equal("original.vi", view.Setting!.ViFilePath);
            Assert.Equal(Input, view.Setting!.InputParameters);
            Assert.Equal(Input, Read(step).InputParameters);
            Assert.Contains("配置已保留", ((System.Windows.Controls.TextBlock)view.FindName("Feedback")).Text);
        }
        finally { File.Delete(path); }
    }

    private static async Task Malformed_online_documents_preserve_the_entire_saved_step()
    {
        const string output = """[[["sum"],{"Name":"sum","Type":"I32","Variable":"Locals.Sum"}]]""";
        string[] invalidDocuments =
        [
            "{\"unexpected\":true}",
            """[[["first"],{"Name":"duplicate","Type":"I32"}],[["second"],{"Name":"duplicate","Type":"I32"}]]""",
            """[[["parent"],{"Name":"parent","Type":"Cluster","ChildNodePath":[["parent","missing"]]}]]""",
            """[[["out"],{"Name":"out","Type":"I32","isMissing":"false"}]]"""
        ];
        foreach (string invalid in invalidDocuments)
        {
            var step = MakeStep("original.vi", Input);
            var original = Read(step);
            original.OutputParameters = output;
            step.StepSetting.Setting = new LabVIEWCallStepPlugin().CreateSerializer().Serialize(original);
            byte[] saved = step.StepSetting.Setting.ToArray();
            var view = new LabVIEWCallEditorView(step, null, (_, _, _, _, _) => new ConnectPanelInfo
            {
                VIState = VIState.Idle,
                ControlOut = """[[["different"],{"Name":"different","Type":"Double","Value":"12.5"}]]""",
                IndicatorOut = invalid
            }, _ => true);
            view.RefreshFromStep(step);
            await view.LoadPanelAsync(true, "candidate.vi");
            Assert.Equal("original.vi", view.Setting!.ViFilePath);
            Assert.Equal(Input, view.Setting.InputParameters);
            Assert.Equal(output, view.Setting.OutputParameters);
            Assert.Equal(saved, step.StepSetting.Setting);
            Assert.Contains("配置已保留", ((System.Windows.Controls.TextBlock)view.FindName("Feedback")).Text);
        }
    }

    private static async Task Editing_the_same_step_during_load_prevents_late_candidate_publication()
    {
        var step = MakeStep("original.vi", Input);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var view = new LabVIEWCallEditorView(step, null, (_, _, _, _, _) =>
        {
            entered.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(5));
            return new ConnectPanelInfo { VIState = VIState.Idle, ControlOut = "[]", IndicatorOut = "[]" };
        }, _ => true);
        view.RefreshFromStep(step);
        var loading = view.LoadPanelAsync(true, "slow-candidate.vi");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            view.Setting!.ViFilePath = "new-selection.vi";
            view.Setting.InputParameters = Input.Replace("42", "99");
        }
        finally { release.Set(); }
        await loading.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("new-selection.vi", view.Setting!.ViFilePath);
        Assert.Equal(Input.Replace("42", "99"), view.Setting.InputParameters);
        await ViCallGate.SharedIdle.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Panel_import_preserves_child_first_order_and_roundtrips_paths_containing_separator()
    {
        const string tree = """[[["root|a","leaf"],{"Name":"value","Type":"I32","Value":"42"}],[["root|a"],{"Name":"root","Type":"Cluster","ChildNodePath":[["root|a","leaf"]]}]]""";
        var root = Assert.Single(LvPanelConverter.ConvertFromJson(tree));
        Assert.Equal("value", Assert.Single(root.Children).Name);
        var restored = Assert.Single(LvPanelConverter.ConvertFromJson(LvPanelConverter.ConvertToJson([root])));
        Assert.Equal("42", Assert.Single(restored.Children).Value);
    }

    private static async Task Missing_node_menu_removes_target_and_updates_only_its_binding()
    {
        var panel = new LvPanelView();
        var nodes = LvPanelConverter.ConvertFromJson(Input);
        var node = nodes.Single();
        node.IsMissing = true;
        node.Log = true;
        panel.ViewModel.ShowSnapshot(nodes, [], null);
        (bool Output, string Json)? published = null;
        panel.ViewModel.BindingsChanged += (output, json) => published = (output, json);
        Assert.True(panel.ViewModel.HasUnsupportedLogs);
        panel.ViewModel.ClearUnsupportedLogsCommand.Execute(null);
        Assert.False(node.Log);
        Assert.False(panel.ViewModel.HasUnsupportedLogs);
        var menu = (System.Windows.Controls.ContextMenu)panel.FindResource("MissingNodeContextMenu");
        menu.PlacementTarget = new System.Windows.Controls.TextBlock { DataContext = node, Tag = panel.ViewModel };
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var remove = (System.Windows.Controls.MenuItem)menu.Items[0];
        Assert.Same(node, remove.CommandParameter);
        remove.Command.Execute(remove.CommandParameter);
        Assert.Empty(panel.ViewModel.ControlNodes);
        Assert.Equal((false, "[]"), published);
    }

    [Fact]
    public async Task Malformed_settings_are_reported_without_resetting_the_step()
    {
        var step = MakeStep("missing.vi");
        var damaged = new byte[] { 0xC1 };
        step.StepSetting.Setting = damaged;
        var errors = await new LabVIEWCallEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = damaged, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [],
            Evaluator = null!, ExecutionContext = null!
        });
        Assert.Contains(errors, x => x.Code == "LV_SETTING_INVALID");
        Assert.Equal(damaged, step.StepSetting.Setting);
    }

    [Fact]
    public async Task Offline_validation_reports_path_and_invalid_parameters_together()
    {
        var setting = new LabVIEWCallSetting { ViFilePath = "missing.vi", InputParameters = """[[["number"],{"Name":"number","Type":"I16","Value":"99999"}]]""" };
        // Simulate a damaged document supplied by an external producer (bypass the writer).
        var step = MakeStep("missing.vi");
        var bytes = MessagePack.MessagePackSerializer.Serialize(setting, MessagePack.MessagePackSerializerOptions.Standard
            .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray));
        var errors = await new LabVIEWCallEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = bytes, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [],
            Evaluator = null!, ExecutionContext = null!
        });
        Assert.Contains(errors, x => x.Code == "LV_VI_PATH_INVALID");
        Assert.Contains(errors, x => x.Code == "LV_PARAMETERS_INVALID");
    }
}
