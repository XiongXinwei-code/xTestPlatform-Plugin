using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Http;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditHttpEditorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_request_options_are_exposed_and_saved_when_the_actual_page_unloads(bool soap) => EthernetDeadlineUiThread.Run(() =>
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        IStepEditorPlugin editor = soap ? new Http.UI.HttpSoapRequestEditorPlugin() : new Http.UI.HttpRequestEditorPlugin();
        var (_, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
        var page = editor.CreateEditor(state.Current.Step, new xTestPlatform.Core.SequenceModels.SequenceFile());
        var window = new Window { Content = page, Width = 1000, Height = 1200, Left = -30000, Top = -30000,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        try
        {
            window.Show(); window.UpdateLayout();
            foreach (string property in soap ? new[] { "LogPayload", "TreatNonSuccessAsFailure", "TreatSoapFaultAsFailure" } : new[] { "LogPayload", "TreatNonSuccessAsFailure" })
            {
                var checkbox = Descendants(page).OfType<CheckBox>().SingleOrDefault(x =>
                    BindingOperations.GetBinding(x, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.Path.Path == property);
                Assert.NotNull(checkbox);
                checkbox.IsChecked = false;
            }
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var setting = plugin.CreateSerializer().Deserialize(state.Current.Step.StepSetting.Setting, state.Current.Step.StepSetting.SettingVersion);
            Assert.False((bool)setting.GetType().GetProperty("LogPayload")!.GetValue(setting)!);
            Assert.False((bool)setting.GetType().GetProperty("TreatNonSuccessAsFailure")!.GetValue(setting)!);
        }
        finally { window.Close(); }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject item)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
        {
            var child = VisualTreeHelper.GetChild(item, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
