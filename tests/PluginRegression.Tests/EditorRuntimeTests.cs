using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Http;
using Http.Models;
using Http.UI.Views;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EditorRuntimeTests
{
    [Fact]
    public void Http_view_binding_survives_immediate_refresh_and_collection_changes() => EthernetDeadlineUiThread.Run(() =>
    {
        var plugin = new HttpClientCreatePlugin();
        var (_, state) = TestContext.Create(plugin, new HttpClientCreateSetting());
        var view = new HttpClientCreateEditorView();
        view.RefreshFromStep(state.Current.Step);
        var window = new Window { Content = view, Width = 900, Height = 650, Left = -30000, Top = -30000,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0, WindowStyle = WindowStyle.None };
        try
        {
            window.Show(); window.UpdateLayout();
            var timeout = Descendants(view).OfType<TextBox>().First(x =>
                x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "TimeoutMs");
            timeout.SetCurrentValue(TextBox.TextProperty, "4321");
            timeout.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            view.ViewModel.ForceConnectionClose = false;
            view.ViewModel.DefaultHeaders.Add(new HttpHeaderItem { Name = "X-Audit", Value = "\"initial\"" });
            view.ViewModel.DefaultHeaders[0].Value = "\"changed\"";
            view.RefreshFromStep(state.Current.Step);
            var serializer = plugin.CreateSerializer();
            var saved = (HttpClientCreateSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, serializer.SettingVersion);
            Assert.Equal(4321, saved.TimeoutMs);
            Assert.False(saved.ForceConnectionClose);
            Assert.Equal("\"changed\"", Assert.Single(saved.DefaultHeaders).Value);

            var tcpPlugin = new Ethernet.TcpOpenPlugin();
            var (_, tcpState) = TestContext.Create(tcpPlugin, new Ethernet.Models.TcpOpenSetting());
            var tcpView = new Ethernet.UI.Views.TcpOpenEditorView();
            tcpView.RefreshFromStep(tcpState.Current.Step);
            window.Content = tcpView;
            window.UpdateLayout();
            var value = Descendants(tcpView).OfType<Syncfusion.Windows.Shared.IntegerTextBox>().Single();
            value.Value = 6543;
            value.GetBindingExpression(Syncfusion.Windows.Shared.IntegerTextBox.ValueProperty)!.UpdateSource();
            tcpView.RefreshFromStep(tcpState.Current.Step);
            var tcpSerializer = tcpPlugin.CreateSerializer();
            var tcpSaved = (Ethernet.Models.TcpOpenSetting)tcpSerializer.Deserialize(tcpState.Current.Step.StepSetting.Setting, tcpSerializer.SettingVersion);
            Assert.Equal(6543, tcpSaved.ConnectTimeoutMs);
            foreach (var closePlugin in new xTestPlatform.Core.Plugins.Contracts.IStepPlugin[]
                     { new Ethernet.TcpClosePlugin(), new Ethernet.DoIP.DoipDisconnectPlugin() })
            {
                var closeSerializer = closePlugin.CreateSerializer();
                var (_, closeState) = TestContext.Create(closePlugin, closeSerializer.CreateDefault());
                var closeView = closePlugin is Ethernet.TcpClosePlugin
                    ? (UserControl)new Ethernet.UI.Views.TcpCloseEditorView()
                    : new Ethernet.UI.Views.DoipDisconnectEditorView();
                var refresh = closeView.GetType().GetMethod("RefreshFromStep")!;
                refresh.Invoke(closeView, [closeState.Current.Step]); window.Content = closeView; window.UpdateLayout();
                var input = Assert.Single(Descendants(closeView).OfType<TextBox>(), x =>
                    x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "TimeoutMs");
                input.Text = "1743"; input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                refresh.Invoke(closeView, [closeState.Current.Step]);
                var savedClose = closeSerializer.Deserialize(closeState.Current.Step.StepSetting.Setting, closeSerializer.SettingVersion);
                Assert.Equal(1743, savedClose.GetType().GetProperty("TimeoutMs")!.GetValue(savedClose));
            }
        }
        finally { window.Close(); }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Http_connection_reuse_setting_reaches_the_actual_client(bool forceClose)
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting { ForceConnectionClose = forceClose,
            // Irrelevant auth expressions must not be evaluated.
            Token = "Locals.doesNotExist", ClientCertPath = "Locals.doesNotExist" });
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(xTestPlatform.Core.Models.TestStatus.Completed, result.StepResult.Status);
        var resource = Assert.Single(state.Items, item => item.Key.StartsWith("Http.Client.", StringComparison.Ordinal)).Value;
        var client = (System.Net.Http.HttpClient)resource.GetType().GetProperty("Client")!.GetValue(resource)!;
        Assert.Equal(forceClose, client.DefaultRequestHeaders.ConnectionClose);
        ((IDisposable)resource).Dispose();
    }
}
