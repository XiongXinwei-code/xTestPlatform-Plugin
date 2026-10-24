using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class DigitalEditorContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DigitalEditorsPassActualSequenceAndEditPositionToEveryExpression(bool output) => UiThread.Run(() =>
    {
        IStepPlugin plugin = output ? new NiDaqDoWritePlugin() : new NiDaqDiReadPlugin();
        IStepEditorPlugin editor = output ? new NiDaq.UI.NiDaqDoWriteEditorPlugin() : new NiDaq.UI.NiDaqDiReadEditorPlugin();
        var sequence = new SequenceFile();
        var step = new Step();
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(plugin.CreateSerializer().CreateDefault());
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        var page = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(step, sequence));
        var sequenceProperty = page.GetType().GetProperty("SequenceFile");
        Assert.NotNull(sequenceProperty);
        Assert.Same(sequence, sequenceProperty.GetValue(page));
        var positionProperty = page.GetType().GetProperty("EditPosition");
        Assert.NotNull(positionProperty);
        var position = new EditPosition();
        positionProperty.SetValue(page, position);
        page.Measure(new Size(900, 500)); page.Arrange(new Rect(0, 0, 900, 500)); page.UpdateLayout();
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var expressions = Descendants(page).Where(control => control.GetType().Name == "ExpressionTextBox").ToArray();
        Assert.Equal(output ? 2 : 1, expressions.Length);
        foreach (var control in expressions)
        {
            Assert.Same(sequence, control.GetType().GetProperty("SequenceFile")!.GetValue(control));
            Assert.Same(position, control.GetType().GetProperty("EditPosition")!.GetValue(control));
        }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
