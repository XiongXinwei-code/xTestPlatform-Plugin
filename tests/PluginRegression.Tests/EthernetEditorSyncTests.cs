using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ethernet;
using Ethernet.Models;
using Ethernet.UI.Views;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetEditorSyncTests
{
    [Fact]
    public void Named_udp_mode_updates_actual_control_enablement_and_serialized_value() => EthernetDeadlineUiThread.Run(() =>
    {
        var plugin = new UdpSendPlugin(); var serializer = plugin.CreateSerializer();
        var (_, state) = TestContext.Create(plugin, new UdpSendSetting());
        var view = new UdpSendEditorView(); view.RefreshFromStep(state.Current.Step); Pump(view);
        var toggle = Assert.IsType<CheckBox>(view.FindName("UseConnectionEditor"));
        var local = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("LocalPortEditor"));
        var connection = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("ConnectionNameEditor"));
        Assert.True(local.IsEnabled); Assert.False(connection.IsEnabled);
        toggle.SetCurrentValue(CheckBox.IsCheckedProperty, true); toggle.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource(); Pump(view);
        Assert.False(local.IsEnabled); Assert.True(connection.IsEnabled);
        view.ViewModel.FlushPendingSave();
        Assert.True(((UdpSendSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 1)).UseConnection);
        toggle.SetCurrentValue(CheckBox.IsCheckedProperty, false); toggle.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource(); Pump(view);
        Assert.True(local.IsEnabled); Assert.False(connection.IsEnabled);
    });

    [Fact]
    public void Delimiter_mode_updates_actual_length_and_delimiter_controls() => EthernetDeadlineUiThread.Run(() =>
    {
        var plugin = new TcpReceivePlugin(); var (_, state) = TestContext.Create(plugin, new TcpReceiveSetting());
        var view = new TcpReceiveEditorView(); view.RefreshFromStep(state.Current.Step); Pump(view);
        var delimiter = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("DelimiterHexEditor"));
        var length = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("ExpectedLengthEditor"));
        Assert.False(delimiter.IsEnabled); Assert.True(length.IsEnabled);
        var toggle = Assert.IsType<CheckBox>(view.FindName("UseDelimiterEditor"));
        toggle.SetCurrentValue(CheckBox.IsCheckedProperty, true); toggle.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource(); Pump(view);
        Assert.True(delimiter.IsEnabled); Assert.False(length.IsEnabled);
        view.ViewModel.FlushPendingSave();
        Assert.True(((TcpReceiveSetting)plugin.CreateSerializer().Deserialize(state.Current.Step.StepSetting.Setting, 1)).UseDelimiter);
    });

    [Fact]
    public void Unloaded_delayed_callback_is_not_saved_and_same_view_reloads_after_loaded() => EthernetDeadlineUiThread.Run(() =>
    {
        var plugin = new UdpOpenPlugin(); var serializer = plugin.CreateSerializer();
        var (_, state) = TestContext.Create(plugin, new UdpOpenSetting { LocalPort = 1234 });
        var view = new UdpOpenEditorView(); view.RefreshFromStep(state.Current.Step);
        view.ViewModel.LocalPort = 4567;
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(4567, ((UdpOpenSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 1)).LocalPort);
        view.ViewModel.LocalPort = 9999; view.ViewModel.FlushPendingSave();
        Assert.Equal(4567, ((UdpOpenSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 1)).LocalPort);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Pump(view);
        Assert.Equal(4567, view.ViewModel.LocalPort);
        view.ViewModel.LocalPort = 5678; view.ViewModel.FlushPendingSave();
        Assert.Equal(5678, ((UdpOpenSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 1)).LocalPort);
    });

    [Fact]
    public void Step_switch_flushes_original_model_and_refreshes_dependent_modes() => EthernetDeadlineUiThread.Run(() =>
    {
        var plugin = new UdpReceivePlugin(); var serializer = plugin.CreateSerializer();
        var (_, first) = TestContext.Create(plugin, new UdpReceiveSetting());
        var (_, second) = TestContext.Create(plugin, new UdpReceiveSetting { UseConnection = true, ConnectionName = "\"other\"" });
        var view = new UdpReceiveEditorView(); view.RefreshFromStep(first.Current.Step); view.ViewModel.LocalPort = 5432;
        view.RefreshFromStep(second.Current.Step); Pump(view);
        Assert.Equal(5432, ((UdpReceiveSetting)serializer.Deserialize(first.Current.Step.StepSetting.Setting, 1)).LocalPort);
        Assert.Equal("\"other\"", view.ViewModel.ConnectionName);
        Assert.False(Assert.IsAssignableFrom<FrameworkElement>(view.FindName("LocalPortEditor")).IsEnabled);
        Assert.True(Assert.IsAssignableFrom<FrameworkElement>(view.FindName("ConnectionNameEditor")).IsEnabled);
    });

    private static void Pump(UserControl view)
    {
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        view.Measure(new Size(1100, 750)); view.Arrange(new Rect(0, 0, 1100, 750));
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }
}
