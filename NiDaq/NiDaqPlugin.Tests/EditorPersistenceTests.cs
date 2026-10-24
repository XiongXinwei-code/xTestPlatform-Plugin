using NiDaq.Models;
using NiDaq.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;
namespace NiDaq.ContractTests;
public class EditorPersistenceTests
{
    [Fact]
    public void ReadPageLoadsAndLaysOutWithoutADriver() => UiThread.Run(() =>
    {
        var page = new NiDaq.UI.Views.NiDaqAiReadEditorView();
        page.Measure(new System.Windows.Size(1000, 700)); page.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
        Assert.True(page.IsMeasureValid);
    });
    [StaFact]
    public void ChannelsPersistOnImmediateRefreshIncludingDirectCollectionEdits()
    {
        var serializer = new NiDaqAiConfigPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new NiDaqAiConfigSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new NiDaqAiConfigViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.Channels.Clear(); vm.Channels.Add(new NiDaqAiChannel { PhysicalChannel = "Dev1/ai0" }); vm.Channels[0].MaxValue = 9;
        vm.AttachStep(step); // 尚未等到 200ms 防抖也必须由刷新 Flush 保存。
        var saved = (NiDaqAiConfigSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(9, Assert.Single(saved.Channels).MaxValue);
    }
}
