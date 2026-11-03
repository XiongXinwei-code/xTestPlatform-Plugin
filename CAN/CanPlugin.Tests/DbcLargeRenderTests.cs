using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.UI.Dbc;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

internal static class DbcLargeRenderTests
{
    private const int MessageCount = 47;
    private const int SignalsPerMessage = 257;
    private const uint FirstId = 291;
    private const string LastValue = "Locals.LastSignal";
    private static readonly Size Viewport = new(1300, 850);

    // The second workflow reopens the first one's actual serialized setting in a fresh STA.
    internal static void VerifyActualPage(bool reopen)
    {
        var savedFile = Path.Combine(AppContext.BaseDirectory, "dbc-large-step-setting.bin");
        var plugin = new DbcWritePlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = reopen ? File.ReadAllBytes(savedFile) : serializer.Serialize(CreateSetting());
        byte[] original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin);
        double attachMs = Measure(() => model.Attach(step));
        var loadClock = Stopwatch.StartNew();
        var heartbeat = PumpModel(model.LoadTask);
        double loadMs = loadClock.Elapsed.TotalMilliseconds;
        Assert.Empty(model.LoadError);
        Assert.Equal(MessageCount * SignalsPerMessage, model.TotalSignalCount);
        Assert.Equal(242, model.SignalPageCount);
        Assert.Equal(50, model.SignalRows.Count);

        var createClock = Stopwatch.StartNew();
        var view = DbcStageQueue.Create(model);
        double createMs = createClock.Elapsed.TotalMilliseconds;
        try
        {
            double firstLayout = Measure(() => Layout(view));
            var grid = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
            var pager = Assert.IsType<StackPanel>(view.FindName("SignalPager"));
            var pageInput = Assert.IsType<TextBox>(view.FindName("SignalPageInput"));
            var moves = new List<object>();
            Assert.Equal(Visibility.Visible, pager.Visibility);
            int firstVisible = CountRows(grid);
            Assert.InRange(firstVisible, 1, 49);
            Assert.Null(view.FindName("ProtectionEditor"));
            void Navigate(string name, int expected)
            {
                double changeMs = Measure(() => Click(view, name));
                double layoutMs = Measure(() => Layout(view));
                Assert.Equal(expected, model.SignalPageNumber);
                Assert.InRange(grid.Items.Count, 1, 50);
                int realized = CountRows(grid);
                Assert.InRange(realized, 1, grid.Items.Count);
                moves.Add(new { Action = name, Page = expected, Projected = grid.Items.Count, Realized = realized,
                    ChangeMs = changeMs, LayoutMs = layoutMs });
            }
            Navigate("SignalNextPage", 2);
            Navigate("SignalPreviousPage", 1);
            pageInput.SetCurrentValue(TextBox.TextProperty, "6");
            Navigate("SignalJumpPage", 6);
            Assert.Equal((FirstId, "S256"), (model.SignalRows[6].MessageId, model.SignalRows[6].Name));
            Assert.Equal((FirstId + 1, "S0"), (model.SignalRows[7].MessageId, model.SignalRows[7].Name));
            Assert.Equal(2, grid.Items.Groups!.Count);
            var ownerBefore = model.SelectedConfiguredMessage;
            var locator = Assert.IsType<ComboBox>(view.FindName("SignalLocateSelector"));
            locator.SelectedItem = model.ConfiguredMessages[23];
            Navigate("SignalLocateButton", 23 * SignalsPerMessage / 50 + 1);
            Assert.Contains(model.SignalRows, row => row.MessageId == FirstId + 23 && row.Name == "S0");
            Assert.Same(ownerBefore, model.SelectedConfiguredMessage);
            Navigate("SignalLastPage", model.SignalPageCount);
            Assert.Equal(29, model.SignalRows.Count);
            Assert.Equal(original, step.StepSetting.Setting);
            var last = model.SignalRows[^1];
            Assert.Equal((FirstId + MessageCount - 1u, "S256"), (last.MessageId, last.Name));
            var column = grid.Columns.Single(c => Equals(c.Header, "发送值表达式"));
            if (reopen)
            {
                Assert.True(last.Selected); Assert.Equal(LastValue, last.Value);
                Assert.Equal((int)DbcValueKind.Raw, last.ValueKind);
                var kind = grid.Columns.Single(c => Equals(c.Header, "值类型"));
                grid.ScrollIntoView(last, kind); Layout(view);
                Assert.Contains(Visuals(kind.GetCellContent(last)!), v => v is TextBlock t && t.Text.Contains("原始值 Raw"));
                grid.ScrollIntoView(last, column); Layout(view);
                Assert.Contains(Visuals(column.GetCellContent(last)!), v => v is TextBlock t && t.Text == LastValue);
            }
            else
            {
                last.Selected = true;
                var kind = grid.Columns.Single(c => Equals(c.Header, "值类型"));
                var choices = DbcRenderTests.EditKind(grid, last, kind, view);
                choices.SetCurrentValue(ComboBox.SelectedIndexProperty, (int)DbcValueKind.Raw);
                Assert.Equal((int)DbcValueKind.Raw, choices.SelectedIndex);
                var editor = DbcRenderTests.EditValue(grid, last, column, view);
                Assert.Equal((int)DbcValueKind.Raw, last.ValueKind);
                editor.GetType().GetProperty("ScriptText")!.SetValue(editor, LastValue);
                // Navigate while the expression is still in edit mode. The page
                // gesture must commit the outgoing cell before its rows disappear.
                Navigate("SignalFirstPage", 1);
                model.Flush();
                var saved = (DbcWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                Assert.Equal(MessageCount, saved.Messages.Count);
                Assert.All(saved.Messages.Take(MessageCount - 1), m => Assert.Empty(m.Signals));
                Assert.Equal(LastValue, Assert.Single(saved.Messages[^1].Signals).Value);
                Assert.Equal(DbcValueKind.Raw, saved.Messages[^1].Signals[0].ValueKind);
                File.WriteAllBytes(savedFile, step.StepSetting.Setting);
            }
            if (reopen) Navigate("SignalFirstPage", 1);
            last.Value = "Locals.StalePage";
            model.Flush();
            Assert.Equal(File.ReadAllBytes(savedFile), step.StepSetting.Setting);
            var data = new { Reopened = reopen, TotalSignals = model.TotalSignalCount, PageCount = model.SignalPageCount,
                PageItems = grid.Items.Count, FirstRealized = firstVisible, AttachMs = attachMs, LoadWallMs = loadMs,
                Heartbeat = heartbeat, CreateMs = createMs, FirstLayoutMs = firstLayout, Navigation = moves };
            var file = reopen ? "dbc-large-reopen.json" : "dbc-large-render.json";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, file), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    }

    private static DbcWriteSetting CreateSetting()
    {
        var text = new StringBuilder();
        var setting = new DbcWriteSetting { UseMessageList = true, TimeoutMs = 60000 };
        for (int msg = 0; msg < MessageCount; msg++)
        {
            uint id = FirstId + (uint)msg;
            text.AppendLine($"BO_ {id} Large{msg}: 48 ECU");
            setting.Messages.Add(new() { MessageId = id });
            for (int s = 0; s < SignalsPerMessage; s++)
                text.AppendLine($" SG_ S{s} : {s}|1@1+ (1,0) [0|1] \"\" ECU");
        }
        setting.Document = new() { Name = "large.dbc", Text = text.ToString() };
        return setting;
    }

    private static object PumpModel(Task load)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        long previous = Stopwatch.GetTimestamp();
        double maxGapMs = 0; int count = 0;
        var pulse = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(20) };
        pulse.Tick += (_, _) =>
        {
            long now = Stopwatch.GetTimestamp();
            maxGapMs = Math.Max(maxGapMs, Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
            previous = now; count++;
        };
        var frame = new DispatcherFrame();
        pulse.Start();
        try
        {
            if (!load.IsCompleted)
            {
                _ = load.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
            }
            load.GetAwaiter().GetResult();
        }
        finally { pulse.Stop(); }
        return new { IntervalMs = 20, Ticks = count, MaxObservedGapMs = Math.Max(maxGapMs, Stopwatch.GetElapsedTime(previous).TotalMilliseconds) };
    }

    private static void Click(FrameworkElement view, string name) =>
        Assert.IsType<Button>(view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(FrameworkElement view)
    { DbcStageQueue.LayoutAndFinish(view, Viewport); }
    private static double Measure(Action action)
    { long started = Stopwatch.GetTimestamp(); action(); return Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
    private static int CountRows(DataGrid grid) => Visuals(grid).OfType<DataGridRow>().Count();
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
