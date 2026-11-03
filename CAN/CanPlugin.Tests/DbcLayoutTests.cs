using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using Syncfusion.Windows.Tools.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

/// <summary>
/// Actual XAML presentation using preloaded view models. This intentionally does
/// not claim to exercise the platform host's asynchronous editor refresh flow.
/// </summary>
internal static class DbcLayoutTests
{
    internal static void VerifyActualPages()
    {
        var loaded = LoadModelsBeforeCreatingAnyViews();
        // From this point onward there are no awaits or Dispatcher message loops.
        // A separate process is used because the unlicensed SDK's delayed modal
        // prevents the full asynchronous host fixture from finishing here.
        foreach (var (plugin, model, name) in loaded)
        {
            var view = DbcStageQueue.Create(model);
            PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
            var tabs = Assert.IsType<TabControlExt>(view.Content);
            Layout(view);
            var messages = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
            Assert.Equal(2, messages.Items.Count);
            Assert.Contains(messages.Columns, column => Equals(column.Header, "该 ID 的 RC / CRC"));
            var signals = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
            Assert.Equal(5, signals.Items.Count);
            Assert.Equal(2, signals.Items.Groups!.Count);
            var read = signals.Columns.Single(column => Equals(column.Header, "读取信号"));
            Assert.Equal(model.IsRead ? Visibility.Visible : Visibility.Collapsed, read.Visibility);
            var cycle = messages.Columns.Single(column => Equals(column.Header, "实际发送周期 / 来源"));
            Assert.Equal(model.IsCyclic ? Visibility.Visible : Visibility.Collapsed, cycle.Visibility);
            if (model.IsCyclic)
            {
                var source = Assert.IsType<TextBlock>(view.FindName("CycleSourceText"));
                Assert.Contains("20 ms", source.Text); Assert.Contains("GenMsgCycleTime", source.Text);
            }
            if (model.IsRead)
            {
                var row = model.SignalRows.Single(signal => signal.Name == "Mode");
                signals.ScrollIntoView(row, read); Layout(view);
                var checkbox = Descendants(read.GetCellContent(row)!).OfType<CheckBox>().Single();
                Assert.False(checkbox.IsChecked); Assert.True(checkbox.IsEnabled);
                var mapped = model.SignalRows.Single(signal => signal.Name == "Voltage");
                var targetColumn = signals.Columns.Single(column => Equals(column.Header, "输出变量（留空不输出）"));
                signals.ScrollIntoView(mapped, targetColumn); Layout(view);
                var target = DbcRenderTests.EditOutput(signals, mapped, targetColumn, view);
                Assert.Equal(model.IsCapture ? "Locals.VoltageSamples" : "Locals.Voltage", target.Text);
            }
            Capture(view, $"{name}-main.png");
            tabs.SelectedIndex = 1; Layout(view);
            var owner = Assert.IsType<TextBlock>(view.FindName("ProtectionOwnerText"));
            Assert.Contains("0x123", owner.Text); Assert.Contains("Sample", owner.Text);
            Assert.Same(model.Protection, ((FrameworkElement)view.FindName("ProtectionEditor")).DataContext);
            Capture(view, $"{name}-protection.png");
            model.SelectedConfiguredMessage = model.ConfiguredMessages[1]; Layout(view);
            Assert.Contains("Extended", owner.Text); Assert.Contains("扩展", owner.Text);
            Assert.False(model.Protection!.RcEnabled);
            Assert.Same(model.Protection, ((FrameworkElement)view.FindName("ProtectionEditor")).DataContext);
            model.Deactivate(); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    }

    private static List<(IStepPlugin Plugin, DbcEditorViewModel Model, string Name)> LoadModelsBeforeCreatingAnyViews()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = LoadAsync();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(20) };
                bool timedOut = false;
                timer.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
                timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); }
                Assert.False(timedOut, "预加载 DBC 模型超过 20 秒，尚未创建任何实际页面");
            }
            return task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task<List<(IStepPlugin Plugin, DbcEditorViewModel Model, string Name)>> LoadAsync()
    {
        var result = new List<(IStepPlugin, DbcEditorViewModel, string)>();
        (IStepPlugin Plugin, string Name)[] plugins = [(new DbcWritePlugin(), "dbc-write"), (new DbcCyclicWritePlugin(), "dbc-cyclic"),
            (new DbcReadPlugin(), "dbc-read"), (new DbcCapturePlugin(), "dbc-capture")];
        foreach (var (plugin, name) in plugins)
        {
            var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
            setting.Document = new() { Name = "layout.dbc", Text = DbcEditorTests.DocumentText + "\nBA_ \"GenMsgCycleTime\" BO_ 291 20;" };
            setting.UseMessageList = true;
            setting.Messages = [new() { MessageId = 0x123, Protection = new() { RcEnabled = true, RcStartByte = 6, RcEndByte = 6,
                CrcEnabled = true, CrcStartByte = 7, CrcEndByte = 7 } }, new() { MessageId = 0x123, FrameType = CanFrameType.Extended }];
            if (setting is DbcReadSetting read)
            {
                read.ResultVariable = "Locals.Json";
                setting.Messages[0].SignalOutputs = [new() { Name = "Mode", Selected = false },
                    new() { Name = "Voltage", TargetVariable = setting is DbcCaptureSetting ? "Locals.VoltageSamples" : "Locals.Voltage" }];
            }
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
            var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
            Assert.False(model.IsLoading); Assert.Empty(model.LoadError);
            result.Add((plugin, model, name));
        }
        return result;
    }

    private static void Layout(FrameworkElement view)
    { DbcStageQueue.LayoutAndFinish(view, new Size(1500, 1080)); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var output = Environment.GetEnvironmentVariable("CAN_EDITOR_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
    }
}
