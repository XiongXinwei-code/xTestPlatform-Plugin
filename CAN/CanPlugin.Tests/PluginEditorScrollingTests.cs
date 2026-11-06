using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using PluginCommon.UI;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class PluginEditorScrollingTests
{
    // These standard WPF windows need to process real Loaded/Unloaded notifications on their own STA.
    [StaFact]
    public void Standard_windows_preserve_scroll_layout_routing_theme_and_input_lifecycle()
    {
        Overlay_template_gets_a_real_row_and_restores_it_on_exit();
        Shift_routing_scrolls_the_nearest_viewer_and_hands_over_at_the_edge();
        Loaded_window_reapplies_theme_and_unload_removes_queued_decoration();
        Single_line_input_hides_only_its_bar_and_restores_host_settings();
    }

    private static void Overlay_template_gets_a_real_row_and_restores_it_on_exit()
    {
        var template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollViewer">
              <Grid x:Name="LayoutRoot">
                <ScrollContentPresenter x:Name="PART_ScrollContentPresenter" Content="{TemplateBinding Content}"/>
                <ScrollBar x:Name="PART_HorizontalScrollBar" Orientation="Horizontal" Height="12" VerticalAlignment="Bottom"
                    Minimum="0" Maximum="{TemplateBinding ScrollableWidth}" ViewportSize="{TemplateBinding ViewportWidth}"
                    Value="{Binding HorizontalOffset, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"/>
              </Grid>
            </ControlTemplate>
            """);
        var viewer = new ScrollViewer { Width = 300, Height = 100, Template = template,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 900, Height = 160 } };
        var root = new Grid(); root.Children.Add(viewer);
        PluginEditorScrolling.SetEnabled(root, true);
        Layout(root);
        var bar = (ScrollBar)template.FindName("PART_HorizontalScrollBar", viewer);
        var presenter = (ScrollContentPresenter)template.FindName("PART_ScrollContentPresenter", viewer);
        var grid = (Grid)template.FindName("LayoutRoot", viewer);
        var originalStyle = bar.Style;
        Assert.True(PluginEditorScrolling.GetEnabled(bar));
        for (int reload = 0; reload < 2; reload++)
        {
            bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout(root);
            Assert.Equal(2, grid.RowDefinitions.Count);
            Assert.Equal(1, Grid.GetRow(bar));
            Point contentBottom = presenter.TranslatePoint(new Point(0, presenter.ActualHeight), grid);
            Point barTop = bar.TranslatePoint(new Point(0, 0), grid);
            Assert.True(contentBottom.Y <= barTop.Y + 0.01, $"内容下缘={contentBottom.Y}, 滚动条上缘={barTop.Y}");
            Assert.Equal(12d, bar.Height);
            var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
            Assert.Equal(ScrollBar.PageLeftCommand, track.DecreaseRepeatButton.Command);
            Assert.Equal(ScrollBar.PageRightCommand, track.IncreaseRepeatButton.Command);
            bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Empty(grid.RowDefinitions); Assert.Equal(0, Grid.GetRow(bar)); Assert.Same(originalStyle, bar.Style);
        }
        PluginEditorScrolling.SetEnabled(root, false);
        bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.Empty(grid.RowDefinitions);
    }

    private static void Shift_routing_scrolls_the_nearest_viewer_and_hands_over_at_the_edge()
    {
        var content = new Border { Width = 900, Height = 60 };
        var inner = new ScrollViewer { Width = 200, Height = 80, Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var canvas = new Canvas { Width = 1000, Height = 400 }; canvas.Children.Add(inner);
        var outer = new ScrollViewer { Width = 300, Height = 150, Content = canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new Grid(); root.Children.Add(outer); PluginEditorScrolling.SetEnabled(root, true);
        Layout(root);
        Assert.True(PluginEditorScrolling.ScrollHorizontal(root, content, -120)); Layout(root);
        Assert.True(inner.HorizontalOffset > 0); Assert.Equal(0d, outer.HorizontalOffset);
        inner.ScrollToRightEnd(); Layout(root);
        Assert.True(PluginEditorScrolling.ScrollHorizontal(root, content, -120)); Layout(root);
        Assert.True(outer.HorizontalOffset > 0);
        outer.ScrollToRightEnd(); Layout(root);
        Assert.False(PluginEditorScrolling.ScrollHorizontal(root, content, -120));
        Assert.False(PluginEditorScrolling.ScrollHorizontal(root, new Border(), -120));
        Assert.False(PluginEditorScrolling.ScrollHorizontal(root, content, 0));
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
            { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = content };
            root.RaiseEvent(wheel);
            Assert.False(wheel.Handled); // Ordinary vertical-wheel behavior remains with the control/theme.
        }
        PluginEditorScrolling.SetEnabled(root, false);
        Assert.False(PluginEditorScrolling.ScrollHorizontal(root, content, 120));
        PluginEditorScrolling.SetEnabled(root, true);
        root.ClearValue(PluginEditorScrolling.EnabledProperty);
        Assert.False(PluginEditorScrolling.GetEnabled(root));
        Assert.False(PluginEditorScrolling.ScrollHorizontal(root, content, 120));
        PluginEditorScrolling.SetEnabled(root, true);
        Assert.True(PluginEditorScrolling.ScrollHorizontal(root, content, 120)); Layout(root);
        PluginEditorScrolling.SetEnabled(root, false);
    }

    private static void Layout(FrameworkElement root)
    { root.Measure(new Size(900, 620)); root.Arrange(new Rect(0, 0, 900, 620)); root.UpdateLayout(); }

    private static void Loaded_window_reapplies_theme_and_unload_removes_queued_decoration()
    {
        var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 1200, Height = 400 } };
        var root = new Grid(); root.Children.Add(viewer); PluginEditorScrolling.SetEnabled(root, true);
        var window = new Window { Content = root, Width = 360, Height = 180, Left = -10000, Top = -10000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
        ScrollBar? bar = null;
        Style? replacement = null;
        ControlTemplate? replacementTemplate = null;
        try
        {
            window.Show(); DrainDispatcher();
            bar = (ScrollBar)viewer.Template.FindName("PART_HorizontalScrollBar", viewer);
            Assert.True(bar.IsLoaded); Assert.True(PluginEditorScrolling.GetEnabled(bar));
            replacement = new Style(typeof(ScrollBar));
            replacement.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.DarkBlue));
            bar.Style = replacement; DrainDispatcher();
            Assert.Same(replacement, bar.Style.BasedOn);
            Assert.Equal(12d, bar.Height);
            bar.ApplyTemplate();
            var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
            track.Thumb.ApplyTemplate();
            var grip = (Border)track.Thumb.Template.FindName("Grip", track.Thumb);
            root.Resources[SystemParameters.HighContrastKey] = true; DrainDispatcher();
            Assert.Equal(1d, grip.Opacity);
            root.Resources[SystemParameters.HighContrastKey] = false; DrainDispatcher();
            Assert.Equal(0.4d, grip.Opacity);

            // A theme may replace Template independently of Style. Keep that host
            // template for restoration, but retain the scoped decoration while enabled.
            replacementTemplate = (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ScrollBar"><Border Background="Transparent"/></ControlTemplate>
                """);
            bar.Template = replacementTemplate; DrainDispatcher();
            Assert.NotSame(replacementTemplate, bar.Template);
            bar.ApplyTemplate(); Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
            // Queue one further theme refresh, then close before it executes.
            bar.Style = replacement;
        }
        finally { window.Close(); DrainDispatcher(); }
        Assert.NotNull(bar); Assert.False(bar.IsLoaded);
        Assert.Same(replacement, bar.Style);
        Assert.Same(replacementTemplate, bar.Template);
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Single_line_input_hides_only_its_bar_and_restores_host_settings()
    {
        var single = new TextBox { Height = 24, Text = new string('A', 160), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var multi = new TextBox { Height = 100, AcceptsReturn = true, Text = new string('B', 160) + "\n第二行",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel(); root.Children.Add(single); root.Children.Add(multi);
        var window = new Window { Content = root, Width = 360, Height = 220, Left = -10000, Top = -10000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
        ScrollViewer? inputViewer = null;
        try
        {
            window.Show(); DrainDispatcher();
            inputViewer = (ScrollViewer)single.Template.FindName("PART_ContentHost", single);
            var multiViewer = (ScrollViewer)multi.Template.FindName("PART_ContentHost", multi);
            var original = inputViewer.HorizontalScrollBarVisibility;
            PluginEditorScrolling.SetEnabled(root, true); DrainDispatcher();
            Assert.Equal(ScrollBarVisibility.Hidden, inputViewer.HorizontalScrollBarVisibility);
            window.Content = null; DrainDispatcher();
            Assert.False(inputViewer.IsLoaded);
            PluginEditorScrolling.SetEnabled(root, false);
            window.Content = root; DrainDispatcher();
            Assert.True(inputViewer.IsLoaded);
            Assert.Equal(original, inputViewer.HorizontalScrollBarVisibility);
            PluginEditorScrolling.SetEnabled(root, true); DrainDispatcher();
            Assert.Equal(ScrollBarVisibility.Hidden, inputViewer.HorizontalScrollBarVisibility);
            Assert.NotEqual(ScrollBarVisibility.Hidden, multiViewer.HorizontalScrollBarVisibility);
            var glyph = single.GetRectFromCharacterIndex(0);
            Assert.False(glyph.IsEmpty);
            Assert.True(inputViewer.ViewportHeight >= glyph.Height);
            Assert.Equal(Visibility.Collapsed, ((ScrollBar)inputViewer.Template.FindName("PART_HorizontalScrollBar", inputViewer)).Visibility);
            Assert.Equal(Visibility.Visible, ((ScrollBar)multiViewer.Template.FindName("PART_HorizontalScrollBar", multiViewer)).Visibility);
            PluginEditorScrolling.SetEnabled(root, false); DrainDispatcher();
            Assert.Equal(original, inputViewer.HorizontalScrollBarVisibility);
            PluginEditorScrolling.SetEnabled(root, true); DrainDispatcher();
            Assert.Equal(ScrollBarVisibility.Hidden, inputViewer.HorizontalScrollBarVisibility);
        }
        finally { window.Close(); DrainDispatcher(); }
        Assert.NotNull(inputViewer);
        Assert.Equal(ScrollBarVisibility.Auto, inputViewer.HorizontalScrollBarVisibility);
    }
}
