using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Http;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;
using Http.Models;
using Http.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpCapabilitiesEditorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_request_pages_save_limits_and_refresh_without_stale_controls(bool soap) => EthernetDeadlineUiThread.Run(() =>
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        IStepEditorPlugin editor = soap ? new Http.UI.HttpSoapRequestEditorPlugin() : new Http.UI.HttpRequestEditorPlugin();
        var (_, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
        var page = editor.CreateEditor(state.Current.Step, new xTestPlatform.Core.SequenceModels.SequenceFile());
        var window = new Window { Content = page, Width = 1100, Height = 1200, Left = -30000, Top = -30000,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        try
        {
            window.Show(); window.UpdateLayout();
            var tabs = Descendants(page).OfType<FrameworkElement>().Single(x => x.GetType().Name == "TabControlExt");
            tabs.GetType().GetProperty("SelectedIndex")!.SetValue(tabs, 1);
            window.UpdateLayout();
            SetText(page, "TimeoutMs", "1234");
            SetText(page, "MaxResponseBytes", "4096");
            if (!soap)
            {
                SetText(page, "ResponseHeadersVariable", "Locals.headers");
                var combo = Descendants(page).OfType<ComboBox>().Single(x =>
                    BindingOperations.GetBinding(x, System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.Path.Path == "ResponseFormat");
                combo.SelectedItem = HttpResponseFormat.Hex;
                window.UpdateLayout();
                Assert.Contains(Descendants(page).OfType<TextBlock>(), x => x.Text.Contains("00FEFF"));
            }
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var serializer = plugin.CreateSerializer();
            var saved = serializer.Deserialize(state.Current.Step.StepSetting.Setting, serializer.SettingVersion);
            Assert.Equal(1234, saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved));
            Assert.Equal(4096, saved.GetType().GetProperty("MaxResponseBytes")!.GetValue(saved));
            if (saved is HttpRequestSetting rest)
            {
                Assert.Equal(HttpResponseFormat.Hex, rest.ResponseFormat);
                Assert.Equal("Locals.headers", rest.ResponseHeadersVariable);
                rest.ResponseFormat = HttpResponseFormat.Base64;
                rest.ContentType = BodyContentType.Bytes;
            }
            saved.GetType().GetProperty("TimeoutMs")!.SetValue(saved, 321);
            state.Current.Step.StepSetting.Setting = serializer.Serialize(saved);
            ((IRefreshableEditor)page).RefreshFromStep(state.Current.Step);
            window.UpdateLayout();
            Assert.Equal(321, page.DataContext.GetType().GetProperty("TimeoutMs")!.GetValue(page.DataContext));
            if (!soap)
            {
                var vm = Assert.IsType<HttpRequestViewModel>(page.DataContext);
                Assert.Contains("十六进制", vm.BodyHint);
                Assert.Contains(Descendants(page).OfType<TextBlock>(), x => x.Text.Contains("Base64 字符串"));
            }
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task New_limits_are_serializable_documented_and_validated(bool soap)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        IStepEditorPlugin editor = soap ? new Http.UI.HttpSoapRequestEditorPlugin() : new Http.UI.HttpRequestEditorPlugin();
        var original = plugin.CreateSerializer().Deserialize([0x80], 1);
        Assert.Equal(0, original.GetType().GetProperty("TimeoutMs")!.GetValue(original));
        Assert.Equal(16777216, original.GetType().GetProperty("MaxResponseBytes")!.GetValue(original));
        original.GetType().GetProperty("TimeoutMs")!.SetValue(original, -1);
        original.GetType().GetProperty("MaxResponseBytes")!.SetValue(original, 0);
        Assert.Contains("TimeoutMs", plugin.Description);
        Assert.Contains("MaxResponseBytes", plugin.Description);
        var (context, state) = TestContext.Create(plugin, original);
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = state.Current.Step.StepSetting.Setting,
            Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new xTestPlatform.Core.SequenceModels.SequenceFile(),
            Block = [], CurrentStep = state.Current.Step
        });
        Assert.Contains(errors, error => error.Message.Contains("请求期限"));
        Assert.Contains(errors, error => error.Message.Contains("最大字节数"));
        Assert.Equal(xTestPlatform.Core.Models.TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
    }

    [StaFact]
    public void Body_mode_changes_refresh_visibility_and_instruction_without_erasing_value()
    {
        var plugin = new HttpRequestPlugin();
        var (_, state) = TestContext.Create(plugin, new HttpRequestSetting { Body = "\"01 FE\"" });
        var vm = new HttpRequestViewModel();
        vm.AttachSerializer(plugin.CreateSerializer()); vm.AttachStep(state.Current.Step);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.ContentType = BodyContentType.Bytes;
        Assert.True(vm.HasBody);
        Assert.Contains(nameof(vm.HasBody), changed);
        Assert.Contains(nameof(vm.BodyHint), changed);
        Assert.Contains("十六进制", vm.BodyHint);
        vm.ContentType = BodyContentType.None;
        Assert.False(vm.HasBody);
        Assert.Equal("\"01 FE\"", vm.Body);
        vm.FlushPendingSave();
    }

    private static void SetText(DependencyObject root, string property, string value)
    {
        var text = Descendants(root).OfType<TextBox>().Single(x =>
            BindingOperations.GetBinding(x, TextBox.TextProperty)?.Path.Path == property);
        text.Text = value;
        text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
