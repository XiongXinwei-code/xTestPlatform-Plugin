using System.Windows;
using SerialPort.UI.Views;
using Syncfusion.Windows.Tools.Controls;
using Xunit;

namespace SerialPort.ContractTests;

public sealed class ReauditTabContractTests
{
    [Fact]
    public void Every_query_business_tab_has_its_required_icon_and_dimensions() => UiThread.Run(() =>
    {
        var view = new SerialPortQueryEditorView();
        var tabs = Assert.IsType<TabControlExt>(view.Content);
        Assert.Equal(2, tabs.Items.Count);
        foreach (var item in tabs.Items.OfType<TabItemExt>())
        {
            Assert.NotNull(item.Image);
            Assert.Equal(20d, item.ImageWidth);
            Assert.Equal(20d, item.ImageHeight);
        }
    });
}
