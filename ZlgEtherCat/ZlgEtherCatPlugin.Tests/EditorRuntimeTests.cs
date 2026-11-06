using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;
using Xunit;

namespace ZlgEtherCat.Tests;

[Collection("Zlg editor runtime")]
public sealed class EditorRuntimeTests
{
    [StaFact]
    public void AllActualEditorsLoadTheirExplicitFieldsAndSaveBeforeSwitchOrUnload()
    {
        var assembly = typeof(OpenEditorView).Assembly;
        foreach (var plugin in ZlgStepCatalog.All)
        {
            var serializer = plugin.CreateSerializer();
            var value = (ZlgSetting)serializer.CreateDefault();
            string name = value.GetType().Name.Replace("Setting", "");
            var editor = (IStepEditorPlugin)Activator.CreateInstance(assembly.GetType($"ZlgEtherCat.UI.Editors.{name}EditorPlugin")!)!;
            var first = NewStep(plugin);
            var second = NewStep(plugin);
            var view = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(first, null));
            Assert.IsAssignableFrom<IRefreshableEditor>(view);
            Assert.Equal(plugin.IconPath, editor.IconPath);
            view.Measure(new Size(900, 700));
            view.Arrange(new Rect(0, 0, 900, 700));
            var model = view.DataContext;
            var modelType = model.GetType();
            var bindings = BindingPaths(view).ToHashSet();
            foreach (var field in value.GetType().GetProperties().Where(p => p.CanWrite))
            {
                var edited = modelType.GetProperty(field.Name);
                Assert.NotNull(edited);
                Assert.Equal(field.PropertyType, edited.PropertyType);
                Assert.Equal(field.GetValue(value), edited.GetValue(model));
                if (field.Name == "Operation" && Enum.GetValues(field.PropertyType).Length == 1) continue;
                Assert.True(bindings.Contains(field.Name) || bindings.Contains(field.Name + "Text"), $"缺少字段绑定：{name}.{field.Name}");
            }
            int originalCalls = 0, replacementCalls = 0;
            modelType.GetProperty("ExecuteCommand")!.SetValue(model, (Action<string, Action>)((_, save) => { originalCalls++; save(); }));
            modelType.GetProperty("TimeoutMs")!.SetValue(model, 1234);
            modelType.GetProperty("ExecuteCommand")!.SetValue(model, (Action<string, Action>)((_, save) => { replacementCalls++; save(); }));
            ((IRefreshableEditor)view).RefreshFromStep(second);
            Assert.Equal(1234, ((ZlgSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).TimeoutMs);
            Assert.Equal(1, originalCalls);
            Assert.Equal(0, replacementCalls);
            modelType.GetProperty("TimeoutMs")!.SetValue(model, 2345);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(2345, ((ZlgSetting)serializer.Deserialize(second.StepSetting.Setting, 1)).TimeoutMs);
            Assert.Equal(1, replacementCalls);
            Assert.NotNull(view.GetType().GetField("SequenceFileProperty"));
            Assert.NotNull(view.GetType().GetField("EditPositionProperty"));
        }
    }

    [StaFact]
    public void FailedSaveKeepsOriginalStepAndHostCommandUntilSuccessfulRetry()
    {
        var plugin = new OpenPlugin();
        var first = NewStep(plugin, new OpenSetting { Operation = OpenOperation.Open });
        var second = NewStep(plugin);
        var view = new OpenEditorView();
        view.RefreshFromStep(first);
        bool fail = true;
        view.ExecuteCommand = (_, save) => { save(); if (fail) throw new InvalidOperationException("host refresh failed"); };
        view.ViewModel.Channel = 1;
        Assert.Throws<InvalidOperationException>(view.ViewModel.Flush);
        Assert.Contains("保存失败", view.ViewModel.Error);
        Assert.Throws<InvalidOperationException>(() => view.RefreshFromStep(second));
        int replacement = 0;
        view.ExecuteCommand = (_, save) => { replacement++; save(); };
        fail = false;
        view.ViewModel.Flush();
        Assert.Equal(0, replacement);
        Assert.DoesNotContain("保存失败", view.ViewModel.Error);
        view.RefreshFromStep(second);
        Assert.Equal(0, view.ViewModel.Channel);
        Assert.Equal(1, ((OpenSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1)).Channel);
    }

    [StaFact]
    public void ActualChannelTextBoxCommitsItsBindingBeforeUnload()
    {
        var plugin = new OpenPlugin();
        var step = NewStep(plugin, new OpenSetting { Operation = OpenOperation.Open });
        var view = (OpenEditorView)new ZlgEtherCat.UI.Editors.OpenEditorPlugin().CreateEditor(step, null);
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var box = Assert.Single(Descendants(view).OfType<TextBox>(), control =>
            control.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ChannelText");
        Assert.Same(view.ViewModel, box.DataContext);
        box.Text = "2";
        box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(2, ((OpenSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).Channel);
    }

    [StaFact]
    public void IndependentBeginExitPersistsTheExitDeadlineWithoutAnOperationSelector()
    {
        var plugin = new BeginExitPlugin();
        var step = NewStep(plugin, new BeginExitSetting { Operation = BeginExitOperation.BeginExit });
        var view = (BeginExitEditorView)new ZlgEtherCat.UI.Editors.BeginExitEditorPlugin().CreateEditor(step, null);
        Assert.Null(view.FindName("OperationEditor"));
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var box = Assert.Single(Descendants(view).OfType<TextBox>(), control =>
            control.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "TimeoutMsText");
        Assert.Same(view.ViewModel, box.DataContext);
        box.Text = "1500";
        box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (BeginExitSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(1500, saved.TimeoutMs);
        Assert.Equal(BeginExitOperation.BeginExit, saved.Operation);
        Assert.Equal("ZlgEtherCat_Master_BeginExit", plugin.DisplayName);
        Assert.Contains("只锁存退出，不证明运动已经停止", plugin.Description);
    }

    private static Step NewStep(IStepPlugin plugin, ZlgSetting? setting = null)
    {
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting ?? serializer.CreateDefault());
        return step;
    }

    private static IEnumerable<string> BindingPaths(DependencyObject element)
    {
        var entries = element.GetLocalValueEnumerator();
        while (entries.MoveNext())
        {
            var binding = BindingOperations.GetBinding(element, entries.Current.Property);
            if (binding?.Path?.Path is { } path) yield return path;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
            foreach (var path in BindingPaths(child)) yield return path;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
