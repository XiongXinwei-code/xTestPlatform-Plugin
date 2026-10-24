using SerialPort.Models;
using SerialPort.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;
namespace SerialPort.ContractTests;
public class EditorPersistenceTests
{
    [Fact]
    public void QueryPageLoadsAndLaysOutWithoutADevice() => UiThread.Run(() =>
    {
        var page = new SerialPort.UI.Views.SerialPortQueryEditorView();
        page.Measure(new System.Windows.Size(1000, 700)); page.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
        Assert.True(page.IsMeasureValid);
    });
    [StaFact]
    public void PortNamePersistsBeforeImmediateRefresh()
    {
        var serializer = new SerialPortOpenPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new SerialPortOpenSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new SerialPortOpenViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.PortName = "\"COM9\""; vm.AttachStep(step);
        Assert.Equal("\"COM9\"", vm.PortName);
    }
}
