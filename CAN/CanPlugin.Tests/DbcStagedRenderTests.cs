using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.UI.Dbc;
using Syncfusion.Windows.Tools.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

/// <summary>Drives each posted callback and layout individually in detached component fixtures.
/// A detached element has no presentation source. Its layout is not proof of an on-screen paint.</summary>
internal sealed class DbcStageQueue
{
    private readonly Queue<Action> _callbacks = new();
    private static readonly ConditionalWeakTable<DbcEditorView, DbcStageQueue> Owners = new();
    internal int Count => _callbacks.Count;
    internal bool RejectNextPost { get; set; }
    internal void Post(Action callback, CancellationToken token)
    {
        if (RejectNextPost)
        {
            RejectNextPost = false;
            throw new InvalidOperationException("测试期间无法投递下一批");
        }
        _callbacks.Enqueue(callback);
    }
    internal Action Take() => _callbacks.Dequeue();

    internal static DbcEditorView Create(DbcEditorViewModel model) => Create(model, out _);
    internal static DbcEditorView Create(IStepPlugin plugin) => Create(new DbcEditorViewModel(plugin));
    internal static DbcEditorView Create(DbcEditorViewModel model, out DbcStageQueue queue)
    {
        queue = new DbcStageQueue();
        var view = new DbcEditorView(model, queue.Post);
        Owners.Add(view, queue);
        return view;
    }

    internal static void LayoutOnce(FrameworkElement view, Size size)
    {
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();
    }

    internal static void LayoutAndFinish(FrameworkElement view, Size size)
    {
        LayoutOnce(view, size);
        if (view is DbcEditorView dbc && Owners.TryGetValue(dbc, out var queue))
        {
            queue.Drain(view, size);
            Assert.True(dbc.ControlsReady.IsCompleted);
            dbc.ControlsReady.GetAwaiter().GetResult();
        }
    }

    internal void Drain(FrameworkElement view, Size size)
    {
        for (int turn = 0; _callbacks.Count != 0; turn++)
        {
            Assert.True(turn < 256, "DBC 页面反复调度，没有完成当前批次");
            Take()();
            LayoutOnce(view, size);
        }
    }
}

internal static class DbcStagedRenderTests
{
    private static readonly Size Size = new(1300, 850);

    internal static void VerifyActualPage()
    {
        var plugin = new DbcWritePlugin();
        var setting = new DbcWriteSetting { UseMessageList = true, Messages = [new() { MessageId = 291 }] };
        setting.Document.Text = "BO_ 291 Indexed: 16 ECU\n" + string.Join('\n',
            Enumerable.Range(0, 123).Select(i => $" SG_ S{i} : {i}|1@1+ (1,0) [0|1] \"\" ECU"));
        var step = MakeStep(plugin, setting);
        var model = new DbcEditorViewModel(plugin);
        model.Attach(step);
        PumpModel(model.LoadTask);
        byte[] original = step.StepSetting.Setting.ToArray();
        var view = DbcStageQueue.Create(model, out var turns);
        try
        {
            var tabs = Assert.IsType<TabControlExt>(view.Content);
            var properties = new TabItemExt { Header = "Properties" };
            tabs.Items.Add(properties);
            Assert.NotNull(view.FindName("DbcFilePathBox"));
            Assert.NotNull(view.FindName("SourceInfoText"));
            Assert.Null(view.FindName("MessagesGrid"));
            Assert.Null(view.FindName("SignalsGrid"));
            Assert.Equal(0, view.CompletedStageCount);
            DbcStageQueue.LayoutOnce(view, Size);
            Assert.True(view.ActualHeight > 0);
            Assert.False(view.ControlsReady.IsCompleted);
            Assert.Null(view.FindName("MessagesGrid"));

            turns.Take()();
            DbcStageQueue.LayoutOnce(view, Size);
            Assert.Equal(1, view.CompletedStageCount);
            Assert.NotNull(view.FindName("MessagesGrid"));
            Assert.Null(view.FindName("TimeoutBox"));
            Assert.Null(view.FindName("SignalsGrid"));

            turns.Take()();
            DbcStageQueue.LayoutOnce(view, Size);
            Assert.Equal(2, view.CompletedStageCount);
            Assert.NotNull(view.FindName("TimeoutBox"));
            Assert.Null(view.FindName("SignalsGrid"));

            turns.Take()();
            DbcStageQueue.LayoutOnce(view, Size);
            Assert.Equal(3, view.CompletedStageCount);
            Assert.True(view.ControlsReady.IsCompletedSuccessfully);
            Assert.Same(properties, tabs.Items[tabs.Items.Count - 1]);
            var grid = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
            Assert.Equal(50, model.SignalRows.Count);
            Assert.Empty(grid.Items);
            var obsolete = view.SignalPresenter.PresentationReady;
            Assert.False(obsolete.IsCompleted);
            var obsoleteTurn = turns.Take();
            turns.RejectNextPost = true;
            model.GoToSignalPage(2);
            Assert.True(obsolete.IsCanceled);
            obsoleteTurn();
            Assert.True(view.SignalPresenter.PresentationReady.IsFaulted);
            Assert.Contains("测试期间无法投递下一批", view.SignalPresenter.Error);
            Assert.Empty(grid.Items);
            Assert.IsType<Button>(view.FindName("SignalPresentationRetry")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            turns.Drain(view, Size);
            Assert.True(view.SignalPresenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Equal(50, grid.Items.Count);
            Assert.Equal("S50", model.SignalRows[0].Name);
            Assert.Equal(original, step.StepSetting.Setting);
            var target = grid.Items.Cast<DbcSignalRow>().First();
            target.Selected = true;
            var column = grid.Columns.Single(c => c.Header?.ToString() == "发送值表达式");
            var input = DbcRenderTests.EditValue(grid, target, column, view);
            input.GetType().GetProperty("ScriptText")!.SetValue(input, "Locals.RetryValue");
            // The page button commits the active expression before replacing rows.
            Assert.IsType<Button>(view.FindName("SignalLastPage")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(view.SignalPresenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Equal("S100", model.SignalRows[0].Name);
            Assert.Equal(23, grid.Items.Count);
            Assert.Equal(model.SignalRows.Select(r => r.Name), grid.Items.Cast<DbcSignalRow>().Select(r => r.Name));
            model.Flush();
            var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal("Locals.RetryValue", saved.Messages[0].Signals.Single(s => s.Name == "S50").Value);
        }
        finally { Unload(view); }

        VerifyReplacementAndUnload(plugin);
        VerifySaveWhileConstructionIsQueued(plugin);
        VerifyUnloadDuringSignalMount(plugin, reloadInsideMount: false);
        VerifyUnloadDuringSignalMount(plugin, reloadInsideMount: true);
        VerifyFailureAndRetry(plugin);
        VerifyPostingFailure(plugin);
    }

    private static void VerifyReplacementAndUnload(IStepPlugin plugin)
    {
        var first = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"Old\"" });
        var next = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"New\"" });
        byte[] original = next.StepSetting.Setting.ToArray();
        var view = DbcStageQueue.Create(new DbcEditorViewModel(plugin), out var turns);
        try
        {
            view.RefreshFromStep(first); DbcStageQueue.LayoutOnce(view, Size);
            var obsolete = view.ControlsReady;
            var stale = turns.Take();
            view.RefreshFromStep(next);
            Assert.True(obsolete.IsCanceled);
            stale();
            Assert.Equal(0, view.CompletedStageCount);
            DbcStageQueue.LayoutOnce(view, Size);
            var suspended = view.ControlsReady;
            var pending = turns.Take();
            Unload(view); pending();
            Assert.True(suspended.IsCanceled);
            Assert.Null(view.FindName("MessagesGrid"));
            Assert.Equal(original, next.StepSetting.Setting);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            DbcStageQueue.LayoutAndFinish(view, Size);
            Assert.Equal("\"New\"", view.ViewModel.ConnectionName);
            Assert.Equal(original, next.StepSetting.Setting);
        }
        finally { Unload(view); }
    }

    private static void VerifySaveWhileConstructionIsQueued(IStepPlugin plugin)
    {
        var step = MakeStep(plugin, new DbcWriteSetting());
        var view = DbcStageQueue.Create(new DbcEditorViewModel(plugin), out var turns);
        int commands = 0;
        try
        {
            view.RefreshFromStep(step); DbcStageQueue.LayoutOnce(view, Size);
            var pending = view.ControlsReady;
            Assert.Equal(1, turns.Count);
            view.ExecuteCommand = (_, apply) =>
            {
                apply();
                commands++;
                view.RefreshFromStep(step);
            };
            for (int i = 1; i <= 3; i++)
            {
                view.ViewModel.ConnectionName = $"\"BenchCAN{i}\"";
                view.ViewModel.Flush();
                Assert.Same(pending, view.ControlsReady);
                Assert.False(pending.IsCompleted);
                Assert.Equal(1, turns.Count);
            }
            Assert.Equal(3, commands);
            DbcStageQueue.LayoutAndFinish(view, Size);
            Assert.True(pending.IsCompletedSuccessfully);
            var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal("\"BenchCAN3\"", saved.ConnectionName);
        }
        finally { Unload(view); }
    }

    private static void VerifyUnloadDuringSignalMount(IStepPlugin plugin, bool reloadInsideMount)
    {
        var first = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"First\"" });
        var next = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"Next\"" });
        byte[] original = first.StepSetting.Setting.ToArray();
        var view = DbcStageQueue.Create(new DbcEditorViewModel(plugin), out var turns);
        var host = Assert.IsType<ContentControl>(view.FindName("SignalStageHost"));
        var descriptor = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
        bool interrupted = false;
        void DuringMount(object? sender, EventArgs args)
        {
            if (interrupted || host.Content == null) return;
            interrupted = true;
            Assert.Equal(2, view.CompletedStageCount);
            Assert.NotNull(view.FindName("SignalsGrid"));
            Unload(view);
            view.RefreshFromStep(next);
            if (reloadInsideMount) view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        }
        descriptor.AddValueChanged(host, DuringMount);
        try
        {
            view.RefreshFromStep(first);
            DbcStageQueue.LayoutOnce(view, Size);
            var oldReady = view.ControlsReady;
            turns.Take()(); turns.Take()(); turns.Take()();
            Assert.True(interrupted);
            Assert.True(oldReady.IsCanceled);
            var mountedGrid = view.FindName("SignalsGrid");
            if (!reloadInsideMount)
            {
                Assert.Equal(Visibility.Collapsed, Assert.IsType<DataGrid>(mountedGrid).Visibility);
                Assert.False(view.SignalPresenter.IsPending);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            }
            var currentReady = view.ControlsReady;
            Assert.NotSame(oldReady, currentReady);
            DbcStageQueue.LayoutAndFinish(view, Size);
            Assert.True(currentReady.IsCompletedSuccessfully);
            Assert.Same(mountedGrid, view.FindName("SignalsGrid"));
            Assert.Equal("\"Next\"", view.ViewModel.ConnectionName);
            view.ViewModel.ConnectionName = "\"Edited\"";
            view.ViewModel.Flush();
            var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(
                next.StepSetting.Setting, next.StepSetting.SettingVersion);
            Assert.Equal("\"Edited\"", saved.ConnectionName);
            Assert.Equal(original, first.StepSetting.Setting);
        }
        finally
        {
            descriptor.RemoveValueChanged(host, DuringMount);
            Unload(view);
        }
    }

    private static void VerifyFailureAndRetry(IStepPlugin plugin)
    {
        var view = DbcStageQueue.Create(new DbcEditorViewModel(plugin), out var turns);
        const string key = "SettingsStageTemplate";
        object template = view.Resources[key];
        var step = MakeStep(plugin, new DbcWriteSetting());
        byte[] original = step.StepSetting.Setting.ToArray();
        try
        {
            view.RefreshFromStep(step); view.Resources.Remove(key);
            DbcStageQueue.LayoutOnce(view, Size); turns.Drain(view, Size);
            Assert.True(view.ControlsReady.IsFaulted);
            _ = view.ControlsReady.Exception;
            Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<TextBlock>(view.FindName("StageErrorText")).Text));
            Assert.Null(view.FindName("SignalsGrid"));
            Assert.Equal(original, step.StepSetting.Setting);
            view.Resources[key] = template;
            var retry = Assert.IsType<Button>(view.FindName("RetryStageButton"));
            retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DbcStageQueue.LayoutAndFinish(view, Size);
            Assert.NotNull(view.FindName("SignalsGrid"));
            Assert.Equal(original, step.StepSetting.Setting);
        }
        finally { Unload(view); }
    }

    private static void VerifyPostingFailure(IStepPlugin plugin)
    {
        bool reject = true;
        var queue = new DbcStageQueue();
        var view = new DbcEditorView(new DbcEditorViewModel(plugin), (callback, token) =>
        {
            if (reject) throw new InvalidOperationException("模拟阶段投递失败");
            queue.Post(callback, token);
        });
        try
        {
            var step = MakeStep(plugin, new DbcWriteSetting());
            byte[] original = step.StepSetting.Setting.ToArray();
            view.RefreshFromStep(step); DbcStageQueue.LayoutOnce(view, Size);
            Assert.True(view.ControlsReady.IsFaulted);
            Assert.Contains("模拟阶段投递失败", view.ControlsReady.Exception!.ToString());
            Assert.Contains("模拟阶段投递失败", Assert.IsType<TextBlock>(view.FindName("StageErrorText")).Text);
            Assert.Equal(0, view.CompletedStageCount);
            reject = false;
            Assert.IsType<Button>(view.FindName("RetryStageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            queue.Drain(view, Size);
            Assert.True(view.ControlsReady.IsCompletedSuccessfully);
            Assert.Equal(original, step.StepSetting.Setting);
        }
        finally { Unload(view); }
    }

    internal static void VerifyDispatcherShutdown()
    {
        var plugin = new DbcWritePlugin();
        var view = DbcStageQueue.Create(new DbcEditorViewModel(plugin), out var turns);
        var step = MakeStep(plugin, new DbcWriteSetting());
        byte[] before = step.StepSetting.Setting.ToArray();
        view.RefreshFromStep(step);
        DbcStageQueue.LayoutOnce(view, Size);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var lifetime = view.ControlsReady;
        var pending = turns.Take();
        // This fixture runs as a separate process so terminating the actual WPF
        // dispatcher cannot destroy a dispatcher required by another workflow.
        view.Dispatcher.InvokeShutdown();
        Assert.True(lifetime.IsCanceled);
        pending();
        Assert.Null(view.FindName("MessagesGrid"));
        Assert.False(view.SignalPresenter.IsPending);
        Assert.Equal(before, step.StepSetting.Setting);
    }

    private static Step MakeStep(IStepPlugin plugin, DbcSourceSetting setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }

    private static void Unload(FrameworkElement view) => view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

    private static void PumpModel(Task task)
    {
        if (!task.IsCompleted)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}

public sealed class DbcDispatcherTurnTests
{
    [StaFact]
    public void Input_already_queued_between_two_control_batches_runs_before_the_second_batch()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var post = DbcUiDispatch.For(dispatcher);
        var trace = new List<string>();
        var frame = new DispatcherFrame();
        post(() =>
        {
            trace.Add("first");
            post(() => { trace.Add("second"); frame.Continue = false; }, CancellationToken.None);
            _ = dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => trace.Add("input")));
        }, CancellationToken.None);
        var deadline = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        deadline.Tick += (_, _) => frame.Continue = false;
        deadline.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { deadline.Stop(); }
        Assert.Equal(new[] { "first", "input", "second" }, trace);
    }
}

public sealed class DbcSignalPresentationTests
{
    private static (DbcEditorViewModel Model, Step Step) Create()
    {
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer();
        var setting = new DbcWriteSetting
        {
            Messages = [new() { MessageId = 291, Signals = [new() { Name = "S99", Value = "Locals.Hidden" }] }],
            Document = new() { Text = "BO_ 291 Indexed: 16 ECU\n" + string.Join('\n',
                Enumerable.Range(0, 101).Select(i => $" SG_ S{i} : {i}|1@1+ (1,0) [0|1] \"\" ECU")) }
        };
        var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        var model = new DbcEditorViewModel(plugin);
        model.Attach(step);
        return (model, step);
    }

    private static void Finish(DbcStageQueue queue)
    {
        for (int turn = 0; queue.Count > 0; turn++)
        {
            Assert.True(turn < 64, "50 行的页面没有在有界回合内完成");
            queue.Take()();
        }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_grouped_reset_can_reenter_page_navigation_or_unload(bool unload)
    {
        var (model, step) = Create(); await model.LoadTask;
        byte[] before = step.StepSetting.Setting.ToArray();
        var queue = new DbcStageQueue();
        var presenter = new DbcSignalPagePresenter(model, queue.Post);
        bool reentered = false;
        presenter.ItemsView.CollectionChanged += (_, args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Reset || reentered || presenter.PresentedRowCount == 0) return;
            Assert.Equal(50, presenter.PresentedRowCount);
            reentered = true;
            if (unload) presenter.Deactivate();
            else model.GoToSignalPage(2);
        };
        try
        {
            presenter.Activate();
            var interrupted = presenter.PresentationReady;
            var retired = queue.Take();
            retired();
            Assert.True(reentered);
            Assert.True(interrupted.IsCanceled);
            if (unload)
            {
                Assert.Empty(presenter.ItemsView.Cast<object>());
                model.GoToSignalPage(3);
                presenter.Activate();
            }
            retired();
            Finish(queue);
            Assert.True(presenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Equal(model.SignalRows.Select(r => r.Name), presenter.ItemsView.Cast<DbcSignalRow>().Select(r => r.Name));
            Assert.Equal(unload ? "S100" : "S50", model.SignalRows[0].Name);
            Assert.Equal(before, step.StepSetting.Setting);
        }
        finally { presenter.Deactivate(); model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Already_dispatched_source_notification_cannot_publish_into_a_different_activation(bool resume)
    {
        var (model, _) = Create(); await model.LoadTask;
        var queue = new DbcStageQueue();
        var presenter = new DbcSignalPagePresenter(model, queue.Post);
        Task? resumed = null;
        bool interrupted = false;
        NotifyCollectionChangedEventHandler earlier = (_, _) =>
        {
            if (interrupted) return;
            interrupted = true;
            presenter.Deactivate();
            if (resume)
            {
                presenter.Activate();
                resumed = presenter.PresentationReady;
            }
        };
        model.SignalRows.CollectionChanged += earlier;
        try
        {
            presenter.Activate(); Finish(queue);
            Assert.Equal(50, presenter.PresentedRowCount);
            model.GoToSignalPage(2);
            Assert.True(interrupted);
            Assert.Equal(0, presenter.PresentedRowCount);
            Assert.Equal(resume ? 1 : 0, queue.Count);
            if (resume)
            {
                Assert.Same(resumed, presenter.PresentationReady);
                Assert.False(resumed!.IsCompleted);
            }
            else
            {
                Assert.False(presenter.IsPending);
                presenter.Activate();
            }
            Finish(queue);
            Assert.True(presenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Equal("S50", presenter.ItemsView.Cast<DbcSignalRow>().First().Name);
        }
        finally
        {
            model.SignalRows.CollectionChanged -= earlier;
            presenter.Deactivate();
            model.Deactivate();
        }
    }

    [WpfFact]
    public async Task Locate_requested_inside_a_grouped_reset_runs_after_its_notification_returns()
    {
        var (model, _) = Create();
        await model.LoadTask;
        var queue = new DbcStageQueue();
        var presenter = new DbcSignalPagePresenter(model, queue.Post);
        bool insideReset = false;
        var located = new List<string>();
        presenter.ItemsView.CollectionChanged += (_, e) =>
        {
            if (e.Action != NotifyCollectionChangedAction.Reset || presenter.PresentedRowCount == 0) return;
            insideReset = true;
            try
            {
                presenter.BringRequestedRow(model.SignalRows[^1], row =>
                {
                    Assert.False(insideReset);
                    Assert.Equal(50, presenter.PresentedRowCount);
                    located.Add(row.Name);
                });
            }
            finally { insideReset = false; }
        };
        try
        {
            presenter.Activate();
            Finish(queue);
            await presenter.PresentationReady;
            Assert.Equal(new[] { "S49" }, located);
        }
        finally { presenter.Deactivate(); model.Deactivate(); }
    }

    [WpfFact]
    public async Task Posting_failure_is_visible_and_retriable_without_touching_settings()
    {
        var (model, step) = Create(); await model.LoadTask;
        byte[] before = step.StepSetting.Setting.ToArray();
        var queue = new DbcStageQueue();
        bool reject = true;
        var presenter = new DbcSignalPagePresenter(model, (callback, token) =>
        {
            if (reject) throw new InvalidOperationException("测试投递失败");
            queue.Post(callback, token);
        });
        try
        {
            presenter.Activate();
            await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.PresentationReady);
            Assert.Contains("测试投递失败", presenter.Error);
            Assert.Contains("测试投递失败", presenter.Status);
            Assert.False(presenter.IsPending);
            Assert.Equal(0, presenter.PresentedRowCount);
            reject = false; presenter.Retry();
            Assert.True(presenter.IsPending);
            Assert.Equal(1, queue.Count);
            queue.Take()();
            Assert.Equal(0, queue.Count);
            Assert.True(presenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Empty(presenter.Error);
            Assert.Equal(50, presenter.PresentedRowCount);
            Assert.Equal(before, step.StepSetting.Setting);
        }
        finally { presenter.Deactivate(); model.Deactivate(); }
    }

    [WpfFact]
    public async Task Only_the_latest_complete_page_can_locate_or_edit_and_hidden_mappings_survive()
    {
        var (model, step) = Create(); await model.LoadTask;
        var queue = new DbcStageQueue();
        var presenter = new DbcSignalPagePresenter(model, queue.Post);
        try
        {
            var notifications = new List<(NotifyCollectionChangedAction Action, int Count)>();
            presenter.ItemsView.CollectionChanged += (_, e) => notifications.Add((e.Action, presenter.PresentedRowCount));
            presenter.Activate();
            var initialReady = presenter.PresentationReady;
            var retired = queue.Take();
            var old = model.SignalRows[0];
            old.Selected = true; old.Value = "Locals.First";
            int visits = 0;
            presenter.BringRequestedRow(model.SignalRows[^1], _ => visits++);
            model.GoToSignalPage(2);
            Assert.True(initialReady.IsCanceled);
            presenter.BringRequestedRow(model.SignalRows[^1], row =>
            {
                Assert.Equal("S99", row.Name);
                Assert.Equal(50, presenter.PresentedRowCount);
                visits += 10;
            });
            retired();
            Assert.Equal(0, visits);
            Finish(queue);
            Assert.Equal(10, visits);
            Assert.Equal(new[] { (NotifyCollectionChangedAction.Reset, 50) }, notifications.Where(n => n.Count > 0));
            notifications.Clear();
            model.GoToSignalPage(3);
            Assert.True(presenter.PresentationReady.IsCompletedSuccessfully);
            Assert.Equal(0, queue.Count);
            Assert.Equal(new[] { (NotifyCollectionChangedAction.Reset, 1) }, notifications);
            old.Value = "Locals.Stale"; model.Flush();
            var saved = (DbcWriteSetting)new DbcWritePlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal("Locals.First", saved.Messages[0].Signals.Single(s => s.Name == "S0").Value);
            Assert.Equal("Locals.Hidden", saved.Messages[0].Signals.Single(s => s.Name == "S99").Value);
        }
        finally { presenter.Deactivate(); model.Deactivate(); }
    }
}
