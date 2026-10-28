using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ethernet;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetSendDeadlineEditorTests
{
    [Fact]
    public void Actual_send_timeout_controls_flush_before_refresh_and_when_unloaded() => EthernetDeadlineUiThread.Run(() =>
    {
        IStepPlugin[] plugins = [new UdpSendPlugin(), new SomeIpFireAndForgetPlugin()];
        UserControl[] views = [new UdpSendEditorView(), new SomeIpFireAndForgetEditorView()];
            for (int i = 0; i < views.Length; i++)
            {
                var serializer = plugins[i].CreateSerializer();
                var (_, state) = TestContext.Create(plugins[i], serializer.CreateDefault());
                var view = views[i]; var editor = (IRefreshableEditor)view;
                editor.RefreshFromStep(state.Current.Step); Pump(view);
                Assert.True(view.FindName("SendTimeoutBox") is TextBox, view.GetType().Name + " 缺少实际发送超时输入框");
                var input = Assert.IsType<TextBox>(view.FindName("SendTimeoutBox"));
                Assert.Equal("SendTimeoutMs", input.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                input.SetCurrentValue(TextBox.TextProperty, "1731");
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                editor.RefreshFromStep(state.Current.Step); Pump(view);
                Assert.Equal(1731, ReadTimeout());

                input = Assert.IsType<TextBox>(view.FindName("SendTimeoutBox"));
                input.SetCurrentValue(TextBox.TextProperty, "2849");
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Equal(2849, ReadTimeout());

                int ReadTimeout()
                {
                    var saved = serializer.Deserialize(state.Current.Step.StepSetting.Setting, serializer.SettingVersion);
                    return (int)saved.GetType().GetProperty("SendTimeoutMs")!.GetValue(saved)!;
                }
            }
    });

    private static void Pump(UserControl view)
    {
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        view.Measure(new Size(1100, 750)); view.Arrange(new Rect(0, 0, 1100, 750));
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert.True(view.IsMeasureValid);
    }
}

internal static class EthernetDeadlineUiThread
{
    private static readonly Lazy<Dispatcher> Owner = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); })
        { IsBackground = true, Name = "Ethernet deadline editor tests" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    });
    public static void Run(Action action) => Owner.Value.Invoke(action);
}
