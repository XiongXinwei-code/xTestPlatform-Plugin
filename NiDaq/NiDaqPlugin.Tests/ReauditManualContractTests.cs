using System.Collections;
using System.Windows;
using NiDaq.UI;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ReauditManualContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualConfigurationBusinessTabsAllExposeIconsAndDimensions(bool synchronous) => UiThread.Run(() =>
    {
        IStepPlugin plugin = synchronous ? new NiDaqSyncConfigPlugin() : new NiDaqAiConfigPlugin();
        IStepEditorPlugin editor = synchronous ? new NiDaqSyncConfigEditorPlugin() : new NiDaqAiConfigEditorPlugin();
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var page = (System.Windows.Controls.UserControl)editor.CreateEditor(step, new SequenceFile());
        page.Measure(new Size(1200, 800)); page.Arrange(new Rect(0, 0, 1200, 800)); page.UpdateLayout();
        var tabs = ((IEnumerable)page.Content.GetType().GetProperty("Items")!.GetValue(page.Content)!).Cast<object>().ToArray();
        Assert.True(tabs.Length >= 4);
        foreach (var tab in tabs)
        {
            Assert.NotNull(tab.GetType().GetProperty("Image")!.GetValue(tab));
            Assert.Equal(20d, Convert.ToDouble(tab.GetType().GetProperty("ImageHeight")!.GetValue(tab)));
            Assert.Equal(20d, Convert.ToDouble(tab.GetType().GetProperty("ImageWidth")!.GetValue(tab)));
        }
    });

    [Fact]
    public void StopDescriptionExplicitlyDocumentsMissingTaskIsAnError()
    {
        var text = new NiDaqTaskStopPlugin().Description;
        Assert.Contains("TaskName", text);
        Assert.Contains("任务不存在时", text);
        Assert.Contains("Error", text);
    }
}
