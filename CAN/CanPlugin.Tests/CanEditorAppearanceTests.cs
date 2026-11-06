using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CAN.CANopen.Cyclic;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Appearance;
using CAN.UI.Dbc;
using CAN.UI.Views;
using Syncfusion.Windows.Tools.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CanEditorAppearanceTests
{
    [Fact]
    public void Dbc_toolbox_leaf_names_identify_DBC_without_changing_step_identity()
    {
        (IStepPlugin Plugin, string Id, string Leaf)[] cases =
        [
            (new DbcReadPlugin(), "IO.CanDbcRead", "DBC Read"),
            (new DbcWritePlugin(), "IO.CanDbcWrite", "DBC Write"),
            (new DbcCyclicWritePlugin(), "IO.CanDbcCyclicWrite", "DBC Cyclic Write"),
            (new DbcCapturePlugin(), "IO.CanDbcCapture", "DBC Capture"),
            (new DbcReadBackgroundStartPlugin(), "IO.CanDbcReadBackgroundStart", "DBC Listen Start"),
            (new DbcReadBackgroundStopPlugin(), "IO.CanDbcReadBackgroundStop", "DBC Listen Stop")
        ];
        foreach (var entry in cases)
        {
            Assert.Equal(entry.Id, entry.Plugin.StepTypeId);
            Assert.Equal(new[] { "CAN", "DBC", entry.Leaf }, entry.Plugin.DisplayName.Split('_'));
        }
    }

    internal static void VerifyActualPages()
    {
        var write = new CanWriteEditorView();
        write.RefreshFromStep(Step(new CanWritePlugin(), new CanWriteSetting { Messages =
            [new() { CanId = "\"0x123\"", Data = "\"00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF\"", UseFdFrame = true }] }));
        var tabs = Assert.IsType<TabControlExt>(write.Content);
        var headers = tabs.Items.Cast<TabItemExt>().ToArray();
        Assert.All(headers, tab => { Assert.Equal(DependencyProperty.UnsetValue, tab.ReadLocalValue(FrameworkElement.StyleProperty)); Assert.NotNull(tab.Image); Assert.Equal(20d, tab.ImageWidth); });
        Layout(write); Capture(write, "can-write-tabs.png");
        tabs.SelectedIndex = 1; Layout(write); Assert.True(headers[1].IsEnabled); Capture(write, "can-write-protection.png");
        write.ViewModel.UseMessageList = true; Assert.False(headers[1].IsEnabled);
        tabs.SelectedIndex = 2; Layout(write);
        VerifyList(Assert.IsType<DataGrid>(write.FindName("MessagesGrid")));
        VerifyLongDataCell(Assert.IsType<DataGrid>(write.FindName("MessagesGrid")));
        Layout(write); Capture(write, "can-write-list.png");
        write.ViewModel.UseMessageList = false; Assert.True(headers[1].IsEnabled);

        var cyclic = new CanCyclicSendStartEditorView();
        // Existing DataGrid appearance and the editor-wide behavior share one owner.
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(cyclic, true);
        cyclic.RefreshFromStep(Step(new CanCyclicSendStartPlugin(), new CanCyclicSendStartSetting { Messages = [new() { Data = "\"00 11 22 33 44 55 66 77\"" }] }));
        Layout(cyclic); VerifyList(Assert.IsType<DataGrid>(cyclic.FindName("MessagesGrid"))); Capture(cyclic, "can-cyclic-list.png");
        Assert.True(((DataGrid)cyclic.FindName("MessagesGrid")).Columns.Single(c => Equals(c.Header, "Data (Hex) 表达式")).ActualWidth >= 240);
        Assert.Equal("发送条件", ((DataGrid)cyclic.FindName("MessagesGrid")).Columns[0].Header);
        var stop = new CanCyclicSendStopEditorView(); Layout(stop);
        Assert.Contains("DBC Cyclic Write", ((TextBlock)stop.FindName("SharedTaskStopHint")).Text);
        Assert.Contains("DbcTask1", ((TextBlock)stop.FindName("SharedTaskStopHint")).Text);

        var dbc = DbcStageQueue.Create(new DbcWritePlugin());
        var message = DbcDatabase.Parse(DbcEditorTests.DocumentText).Find(0x123, false);
        foreach (var signal in message.Signals) dbc.ViewModel.SignalRows.Add(new DbcSignalRow(signal, new DbcSignalInput { Name = signal.Name, Value = "12.5" }, new()));
        Layout(dbc); VerifyList(Assert.IsType<DataGrid>(dbc.FindName("SignalsGrid"))); Capture(dbc, "can-dbc-list.png");
        Assert.Equal("DBC Write", ((TabItemExt)((TabControlExt)dbc.Content).Items[0]).Header);

        var service = new CyclicServiceEditorView(new ServiceStartPlugin());
        service.RefreshFromStep(Step(new ServiceStartPlugin(), new CyclicSetting())); service.ViewModel.AddRoute();
        Layout(service); VerifyList(Assert.IsType<DataGrid>(service.FindName("RoutesGrid")));
        var targets = new CyclicServiceEditorView(new CycleStartPlugin());
        targets.RefreshFromStep(Step(new CycleStartPlugin(), new CyclicSetting())); targets.ViewModel.AddTarget();
        Layout(targets); VerifyList(Assert.IsType<DataGrid>(targets.FindName("TargetsGrid")));

        var flash = new CanFlashEditorView(); Layout(flash);
        Assert.True(CanListAppearance.GetEnabled(Descendants(flash).OfType<ListBox>().Single()));
        foreach (var view in new FrameworkElement[] { write, cyclic, stop, dbc, service, targets, flash })
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private static void VerifyList(DataGrid grid)
    {
        Assert.True(CanListAppearance.GetEnabled(grid)); Assert.True(grid.MinRowHeight >= 36);
        var bars = Descendants(grid).OfType<ScrollBar>().ToArray();
        Assert.Contains(bars, b => b.Orientation == Orientation.Horizontal);
        foreach (var bar in bars)
        {
            var original = bar.Style;
            bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); bar.ApplyTemplate(); bar.UpdateLayout();
            if (bar.Orientation == Orientation.Vertical) { Assert.Same(original, bar.Style); continue; }
            Assert.Equal(12d, bar.Height);
            var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
            Assert.NotNull(track.Thumb); track.Thumb.ApplyTemplate();
            Assert.True(track.Thumb.Template.FindName("Grip", track.Thumb) is Border,
                "Horizontal thumb template: " + System.Windows.Markup.XamlWriter.Save(track.Thumb.Template));
            var grip = (Border)track.Thumb.Template.FindName("Grip", track.Thumb);
            Assert.Equal(track.Thumb.IsEnabled ? 0.4 : 0.2, grip.Opacity);
            Assert.Equal(ScrollBar.PageLeftCommand, track.DecreaseRepeatButton.Command);
            Assert.Equal(ScrollBar.PageRightCommand, track.IncreaseRepeatButton.Command);
            if (bar.Maximum > 0) { bar.Value = bar.Maximum / 2; Assert.Equal(bar.Value, track.Value); }
            bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Assert.Same(original, bar.Style);
            bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Assert.Equal(12d, bar.Height);
        }
    }

    private static void VerifyLongDataCell(DataGrid grid)
    {
        const string expected = "\"00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF\"";
        var editor = Descendants(grid).OfType<ICSharpCode.AvalonEdit.TextEditor>().Single(x => x.Text == expected);
        var viewer = Descendants(editor).OfType<ScrollViewer>().First();
        viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout(grid);
        Assert.Equal(ScrollBarVisibility.Hidden, viewer.HorizontalScrollBarVisibility);
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();
        Assert.NotEmpty(textView.VisualLines);
        Assert.True(viewer.ViewportHeight >= textView.DefaultLineHeight,
            $"实际 Data 单元格文字被裁切：视口={viewer.ViewportHeight}, 行高={textView.DefaultLineHeight}");
        Assert.True(textView.ActualHeight >= textView.DefaultLineHeight);
        var bar = (ScrollBar)viewer.Template.FindName("PART_HorizontalScrollBar", viewer);
        Assert.Equal(Visibility.Collapsed, bar.Visibility);
        viewer.ScrollToHorizontalOffset(0); Layout(grid);
        Assert.True(PluginCommon.UI.PluginEditorScrolling.ScrollHorizontal(grid, textView, -120)); Layout(grid);
        Assert.True(viewer.HorizontalOffset > 0);
        Assert.Equal(expected, editor.Text);
        viewer.ScrollToHorizontalOffset(0); Layout(grid);
        // The table's own scrollbar is independent of the hidden input scrollbar.
        var tableViewer = Descendants(grid).OfType<ScrollViewer>().First();
        Assert.NotEqual(ScrollBarVisibility.Hidden, tableViewer.HorizontalScrollBarVisibility);
    }

    private static Step Step(IStepPlugin plugin, object setting)
    { var result = new Step(); var serializer = plugin.CreateSerializer(); result.StepSetting.Setting = serializer.Serialize(setting); result.StepSetting.SettingVersion = serializer.SettingVersion; return result; }
    private static void Layout(FrameworkElement view)
    { DbcStageQueue.LayoutAndFinish(view, new Size(900, 620)); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var output = Environment.GetEnvironmentVariable("CAN_EDITOR_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name)); png.Save(file);
        File.WriteAllLines(Path.Combine(output, name + ".layout.txt"), Descendants(view).OfType<ScrollBar>()
            .Where(b => b.Orientation == Orientation.Horizontal).Select(b =>
                $"{b.Name}: visible={b.Visibility}, size={b.ActualWidth}x{b.ActualHeight}, max={b.Maximum}, viewport={b.ViewportSize}, value={b.Value}"));
    }
}
