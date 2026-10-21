using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CiA402.Models;
using CiA402.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CiA402.Tests;

public sealed class ExplicitEditorTests
{
    [StaFact]
    public void 每个设置字段都有独立强类型属性和可加载的编辑器()
    {
        var assembly = typeof(BindAxisViewModel).Assembly;
        foreach (var plugin in ContractTests.Plugins)
        {
            var setting = plugin.CreateSerializer().CreateDefault();
            var name = setting.GetType().Name.Replace("Setting", "");
            var editor = (IStepEditorPlugin)Activator.CreateInstance(assembly.GetType($"CiA402.UI.Editors.{name}EditorPlugin")!)!;
            var (_, state) = TestContext.Create(plugin, setting);
            var view = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(state.Current!.Step, null));
            Assert.IsAssignableFrom<IRefreshableEditor>(view);
            var model = view.DataContext;
            var bindings = BindingPaths(view).ToHashSet();
            Assert.Equal($"{name}ViewModel", model.GetType().Name);
            foreach (var property in setting.GetType().GetProperties())
            {
                var editorProperty = model.GetType().GetProperty(property.Name);
                Assert.NotNull(editorProperty);
                Assert.Equal(property.PropertyType, editorProperty.PropertyType);
                Assert.Equal(property.GetValue(setting), editorProperty.GetValue(model));
                Assert.Contains(property.Name, bindings);
            }
            Assert.NotNull(view.GetType().GetField("SequenceFileProperty"));
            Assert.NotNull(view.GetType().GetField("EditPositionProperty"));
        }
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
    [StaFact]
    public void 显式布尔绑定保存且撤销回调固定原步骤()
    {
        var plugin = new SetModePlugin();
        var (_, first) = TestContext.Create(plugin, new SetModeSetting());
        var (_, second) = TestContext.Create(plugin, new SetModeSetting());
        var view = new CiA402.UI.Views.SetModeEditorView();
        view.RefreshFromStep(first.Current!.Step);
        Action? save = null;
        view.ExecuteCommand = (_, action) => save = action;
        view.ViewModel.AllowWhileEnabled = true;
        view.ViewModel.Flush();
        Assert.NotNull(save);
        view.RefreshFromStep(second.Current!.Step);
        save();
        Assert.True(((SetModeSetting)plugin.CreateSerializer().Deserialize(first.Current.Step.StepSetting.Setting, 1)).AllowWhileEnabled);
        Assert.False(((SetModeSetting)plugin.CreateSerializer().Deserialize(second.Current.Step.StepSetting.Setting, 1)).AllowWhileEnabled);
    }
}
