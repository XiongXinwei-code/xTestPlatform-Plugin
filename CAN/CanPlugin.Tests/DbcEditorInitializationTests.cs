using CAN.Dbc;
using CAN.UI.Dbc;
using StepEditor.Abstractions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class DbcEditorInitializationTests
{
    private static IStepPlugin[] Plugins() => [new DbcReadPlugin(), new DbcWritePlugin(), new DbcCyclicWritePlugin(),
        new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin(), new DbcReadBackgroundStopPlugin()];

    [WpfFact]
    public void New_step_settings_open_with_platform_initial_metadata_without_changing_saved_bytes()
    {
        IStepPlugin[] plugins = [new DbcReadPlugin(), new DbcWritePlugin(), new DbcCyclicWritePlugin(),
            new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin()];
        foreach (var plugin in plugins)
        {
            var serializer = plugin.CreateSerializer();
            var step = new Step();
            step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            // Model the host's new-step metadata, without pre-stamping the plugin version.
            byte[] original = step.StepSetting.Setting.ToArray();
            var model = new DbcEditorViewModel(plugin);
            model.Attach(step);
            Assert.Empty(model.LoadError);
            Assert.True(model.UseMessageList);
            Assert.Empty(model.ConfiguredMessages);
            model.Deactivate();
            Assert.Equal(original, step.StepSetting.Setting);
        }
    }

    internal static void VerifyActualPages()
    {
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer();
            var step = new Step();
            step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            var editor = typeof(DbcWriteEditorPlugin).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(t))
                .Select(t => (IStepEditorPlugin)Activator.CreateInstance(t)!)
                .Single(e => e.StepTypeId == plugin.StepTypeId);
            var sequence = new SequenceFile();
            var view = editor.CreateEditor(step, sequence);
            Layout(view);
            var model = view.GetType().GetProperty("ViewModel")!.GetValue(view)!;
            Assert.NotNull(Assert.IsAssignableFrom<UserControl>(view).Content);
            if (view is DbcEditorView dbc)
            {
                Assert.NotNull(dbc.FindName("BrowseButton"));
                Assert.Empty(dbc.ViewModel.LoadError);
                Assert.Empty(dbc.ViewModel.ConfiguredMessages);
            }
            bool saved = false;
            Action<string, Action> command = (_, apply) =>
            {
                apply(); saved = true;
                step.PropertiesSetting.General.StepDescription = plugin.GenerateDescription(step.StepSetting.Setting);
                ((IRefreshableEditor)view).RefreshFromStep(step);
            };
            view.GetType().GetProperty("ExecuteCommand")!.SetValue(view, command);
            model.GetType().GetProperty("ConnectionName")!.SetValue(model, "\"DBC_UI_CHECK\"");
            model.GetType().GetMethod("Flush", BindingFlags.Instance | BindingFlags.Public)!.Invoke(model, null);
            Assert.True(saved, plugin.StepTypeId);
            Assert.Contains("DBC_UI_CHECK", step.PropertiesSetting.General.StepDescription);
            byte[] savedBytes = step.StepSetting.Setting.ToArray();
            var stored = serializer.Deserialize(savedBytes, step.StepSetting.SettingVersion);
            Assert.Equal("\"DBC_UI_CHECK\"", stored.GetType().GetProperty("ConnectionName")!.GetValue(stored));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var reopened = editor.CreateEditor(step, sequence);
            Layout(reopened);
            var reopenedModel = reopened.GetType().GetProperty("ViewModel")!.GetValue(reopened)!;
            Assert.Equal("\"DBC_UI_CHECK\"", reopenedModel.GetType().GetProperty("ConnectionName")!.GetValue(reopenedModel));
            reopened.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(savedBytes, step.StepSetting.Setting);
        }
    }

    private static void Layout(FrameworkElement view)
    {
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        view.Measure(new Size(1300, 800));
        view.Arrange(new Rect(0, 0, 1300, 800));
        view.UpdateLayout();
        Assert.True(view.ActualWidth > 0 && view.ActualHeight > 0);
    }
}
