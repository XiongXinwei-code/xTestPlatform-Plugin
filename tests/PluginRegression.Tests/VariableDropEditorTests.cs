using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Http;
using Http.Models;
using StepEditor.Abstractions;
using Xunit;

namespace PluginRegression.Tests;

public sealed class VariableDropEditorTests
{
    [Fact]
    public void Http_mapping_drop_is_saved_to_original_step_before_switching()
        => EthernetDeadlineUiThread.Run(() =>
        {
            var plugin = new HttpJsonExtractPlugin();
            var editor = new Http.UI.HttpJsonExtractEditorPlugin();
            var original = new HttpJsonExtractSetting
            {
                Items = new ObservableCollection<HttpExtractItem>
                {
                    new() { Path = "one", TargetVariable = "Locals.First" },
                    new() { Path = "two", TargetVariable = "Locals.Second" }
                }
            };
            var (_, first) = TestContext.Create(plugin, original);
            var (_, next) = TestContext.Create(plugin, new HttpJsonExtractSetting());
            var page = editor.CreateEditor(first.Current.Step, new xTestPlatform.Core.SequenceModels.SequenceFile());
            var window = new Window
            {
                Content = page, Width = 1100, Height = 900, Left = -30000, Top = -30000,
                ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                var grid = Descendants(page).OfType<DataGrid>().Single();
                var column = grid.Columns.OfType<DataGridTextColumn>()
                    .Single(c => (c.Binding as Binding)?.Path.Path == "TargetVariable");
                object row = grid.Items[1];
                grid.ScrollIntoView(row, column);
                window.UpdateLayout();
                var content = column.GetCellContent(row);
                Assert.NotNull(content);
                var result = Drop(content, "FileGlobals.Result");
                Assert.Equal(DragDropEffects.Copy, result.Effects);
                ((IRefreshableEditor)page).RefreshFromStep(next.Current.Step);
                var serializer = plugin.CreateSerializer();
                var saved = (HttpJsonExtractSetting)serializer.Deserialize(first.Current.Step.StepSetting.Setting, serializer.SettingVersion);
                var nextSaved = (HttpJsonExtractSetting)serializer.Deserialize(next.Current.Step.StepSetting.Setting, serializer.SettingVersion);
                Assert.Equal("Locals.First", saved.Items[0].TargetVariable);
                Assert.Equal("FileGlobals.Result", saved.Items[1].TargetVariable);
                Assert.Empty(nextSaved.Items);
            }
            finally { window.Close(); }
        });

    [Fact]
    public void Http_scalar_drop_updates_the_setting_before_a_step_switch()
        => EthernetDeadlineUiThread.Run(() =>
        {
            var plugin = new HttpRequestPlugin();
            var editor = new Http.UI.HttpRequestEditorPlugin();
            var (_, first) = TestContext.Create(plugin, new HttpRequestSetting());
            var (_, next) = TestContext.Create(plugin, new HttpRequestSetting());
            var page = editor.CreateEditor(first.Current.Step, new xTestPlatform.Core.SequenceModels.SequenceFile());
            var window = new Window
            {
                Content = page, Width = 1100, Height = 1200, Left = -30000, Top = -30000,
                ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                var target = Descendants(page).OfType<TextBox>().Single(x =>
                    BindingOperations.GetBinding(x, TextBox.TextProperty)?.Path.Path == "ResponseVariable");
                Assert.True(target.AllowDrop);
                Assert.Equal(DragDropEffects.Copy, Drop(target, "Locals.Response").Effects);
                ((IRefreshableEditor)page).RefreshFromStep(next.Current.Step);
                var serializer = plugin.CreateSerializer();
                var saved = (HttpRequestSetting)serializer.Deserialize(first.Current.Step.StepSetting.Setting, serializer.SettingVersion);
                Assert.Equal("Locals.Response", saved.ResponseVariable);
            }
            finally { window.Close(); }
        });

    private static DragEventArgs Drop(UIElement target, string text)
    {
        var data = new DataObject(DataFormats.StringFormat, text);
        var constructor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)], null)!;
        var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, target, new Point()]);
        args.RoutedEvent = UIElement.PreviewDropEvent;
        target.RaiseEvent(args);
        return args;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
