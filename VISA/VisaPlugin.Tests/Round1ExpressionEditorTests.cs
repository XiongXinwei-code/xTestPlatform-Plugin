using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StepEditor.Abstractions;
using VISA.Models;
using VISA.UI;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace VISA.Tests;

public sealed class Round1ExpressionEditorTests
{
    [Theory]
    [InlineData("open", 2)]
    [InlineData("close", 1)]
    [InlineData("read", 1)]
    [InlineData("write", 2)]
    [InlineData("query", 2)]
    [InlineData("wait", 1)]
    [InlineData("batch", 2)]
    public void Every_actual_expression_editor_has_explicit_string_result_contract(string kind, int expectedCount) => UiThread.Run(() =>
    {
        (IStepPlugin Plugin, IStepEditorPlugin Editor) pair = kind switch
        {
            "open" => (new VisaOpenPlugin(), new VisaOpenEditorPlugin()),
            "close" => (new VisaClosePlugin(), new VisaCloseEditorPlugin()),
            "read" => (new VisaReadPlugin(), new VisaReadEditorPlugin()),
            "write" => (new VisaWritePlugin(), new VisaWriteEditorPlugin()),
            "query" => (new VisaQueryPlugin(), new VisaQueryEditorPlugin()),
            "wait" => (new VisaWaitOpcPlugin(), new VisaWaitOpcEditorPlugin()),
            _ => (new VisaBatchWritePlugin(), new VisaBatchWriteEditorPlugin())
        };
        var plugin = pair.Plugin; var editor = pair.Editor;
        var serializer = plugin.CreateSerializer(); var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(kind == "batch"
            ? new VisaBatchWriteSetting { Items = [new VisaBatchWriteItem { Command = "\"*CLS\"" }] } : serializer.CreateDefault());
        var view = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(step, new SequenceFile()));
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        view.Measure(new Size(1200, 750)); view.Arrange(new Rect(0, 0, 1200, 750)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var inputs = Find(view).ToArray();
        Assert.Equal(expectedCount, inputs.Length); // Includes the actual batch row DataTemplate.
        Assert.All(inputs, input => Assert.Equal("String", input.ExpectedResultType));
    });

    private static IEnumerable<ExpressionTextBox.ExpressionTextBox> Find(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ExpressionTextBox.ExpressionTextBox match) yield return match;
            foreach (var nested in Find(child)) yield return nested;
        }
    }
}
