using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace OpcUa.Tests;

public sealed class ReauditEditorLifecycleTests
{
    [Theory]
    [InlineData("unload", "NodeId")]
    [InlineData("refresh", "NodeId")]
    [InlineData("switch", "NodeId")]
    [InlineData("unload", "ResultVariable")]
    [InlineData("refresh", "ResultVariable")]
    [InlineData("switch", "ResultVariable")]
    public void BatchReadCommitsActualEditingCellWithoutLostFocus(string transition, string property) => UiThread.Run(() =>
    {
        var plugin = new OpcUaBatchReadPlugin(); var serializer = plugin.CreateSerializer();
        var first = new Step(); first.StepSetting.SettingVersion = serializer.SettingVersion;
        first.StepSetting.Setting = serializer.Serialize(new OpcUaBatchReadSetting
        { Items = [new() { NodeId = "ns=2;s=Before", ResultVariable = "Locals.Before" }] });
        var second = new Step(); second.StepSetting.SettingVersion = serializer.SettingVersion;
        second.StepSetting.Setting = serializer.Serialize(new OpcUaBatchReadSetting
        { Items = [new() { NodeId = "ns=2;s=Other", ResultVariable = "Locals.Other" }] });
        var page = (OpcUaBatchReadEditorView)new UI.OpcUaBatchReadEditorPlugin().CreateEditor(first, new SequenceFile());
        Layout(page);
        var grid = Assert.IsType<DataGrid>(page.FindName("ItemsGrid"));
        var column = Assert.Single(grid.Columns.OfType<DataGridTextColumn>(),
            value => value.Binding is Binding binding && binding.Path.Path == property);
        var item = grid.Items[0]; grid.SelectedItem = item; grid.CurrentCell = new DataGridCellInfo(item, column);
        grid.ScrollIntoView(item, column); grid.UpdateLayout();
        Assert.True(grid.BeginEdit()); grid.UpdateLayout();
        var editor = Assert.IsType<TextBox>(column.GetCellContent(item));
        string replacement = property == "NodeId" ? "ns=2;s=Edited" : "Locals.Edited";
        editor.Text = replacement; // 不直接 UpdateSource，不伪造已失焦/已提交的编辑。

        if (transition == "unload") page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        else ((IRefreshableEditor)page).RefreshFromStep(transition == "switch" ? second : first);

        var saved = (OpcUaBatchReadSetting)serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
        Assert.Equal(replacement, typeof(OpcUaBatchReadItem).GetProperty(property)!.GetValue(saved.Items[0]));
        var unchanged = (OpcUaBatchReadSetting)serializer.Deserialize(second.StepSetting.Setting, second.StepSetting.SettingVersion);
        Assert.Equal("ns=2;s=Other", unchanged.Items[0].NodeId);
        Assert.Equal("Locals.Other", unchanged.Items[0].ResultVariable);
    });

    [Fact]
    public void ConnectUsesMaskedPasswordControlNotOrdinaryBoundText() => UiThread.Run(() =>
    {
        var (_, _, page) = ConnectPage();
        Assert.Single(Logical(page).OfType<PasswordBox>());
        Assert.DoesNotContain(Logical(page).OfType<TextBox>(), box =>
            BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path?.Path == "Password");
    });

    [Theory]
    [InlineData("unload")]
    [InlineData("refresh")]
    [InlineData("switch")]
    [InlineData("viewmodel")]
    public void MaskedCredentialPreservesEditsAcrossEditorTransitions(string transition) => UiThread.Run(() =>
    {
        var (plugin, first, page) = ConnectPage(); var serializer = plugin.CreateSerializer();
        var box = Assert.Single(Logical(page).OfType<PasswordBox>());
        Assert.Equal("FakeCredential-Before", box.Password);
        const string replacement = "FakeCredential-Edited";
        if (transition == "viewmodel") page.ViewModel.Password = replacement;
        else box.Password = replacement;
        Assert.Equal(replacement, page.ViewModel.Password);
        Assert.Equal(replacement, box.Password);

        var other = new Step(); other.StepSetting.SettingVersion = serializer.SettingVersion;
        other.StepSetting.Setting = serializer.Serialize(new OpcUaConnectSetting
        { AuthMode = OpcUaAuthMode.UserPassword, UserName = "FakeUser", Password = "FakeCredential-Other" });
        if (transition == "unload" || transition == "viewmodel") page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        else page.RefreshFromStep(transition == "switch" ? other : first);

        var saved = (OpcUaConnectSetting)serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
        Assert.Equal(replacement, saved.Password);
        var untouched = (OpcUaConnectSetting)serializer.Deserialize(other.StepSetting.Setting, other.StepSetting.SettingVersion);
        Assert.Equal("FakeCredential-Other", untouched.Password);
        if (transition == "switch") Assert.Equal("FakeCredential-Other", box.Password);
    });

    private static (OpcUaConnectPlugin Plugin, Step Step, OpcUaConnectEditorView Page) ConnectPage()
    {
        var plugin = new OpcUaConnectPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaConnectSetting
        { AuthMode = OpcUaAuthMode.UserPassword, UserName = "FakeUser", Password = "FakeCredential-Before" });
        var page = (OpcUaConnectEditorView)new UI.OpcUaConnectEditorPlugin().CreateEditor(step, new SequenceFile());
        Layout(page); return (plugin, step, page);
    }

    private static void Layout(FrameworkElement page)
    { page.Measure(new Size(1200, 800)); page.Arrange(new Rect(0, 0, 1200, 800)); page.UpdateLayout(); }

    private static IEnumerable<object> Logical(object parent)
    {
        yield return parent;
        if (parent is not DependencyObject dependency) yield break;
        foreach (var child in LogicalTreeHelper.GetChildren(dependency))
            foreach (var nested in Logical(child)) yield return nested;
    }
}
