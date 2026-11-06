using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.UI.Models;
using LabVIEWCallPlugin.UI.ViewModels;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class PanelStateTests
{
    [Fact]
    public void ReentrancyIsUnknownUntilOnlineSuccessAndUpdatesForBothDependencies()
    {
        var model = new LvPanelViewModel();
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        model.ShowSnapshot([], [], null);
        Assert.Equal("未确认", model.ReentrantStateText);

        changed.Clear();
        model.IsReentrant = true;
        Assert.Contains(nameof(model.ReentrantStateText), changed);
        Assert.Equal("未确认", model.ReentrantStateText);

        changed.Clear();
        model.LabViewIdeState = LabViewIdeState.Connected;
        Assert.Contains(nameof(model.ReentrantStateText), changed);
        Assert.Equal("Yes", model.ReentrantStateText);

        changed.Clear();
        model.IsReentrant = false;
        Assert.Contains(nameof(model.ReentrantStateText), changed);
        Assert.Equal("No", model.ReentrantStateText);

        model.MarkOnlineFailure("模拟检查失败");
        Assert.Equal("未确认", model.ReentrantStateText);
        model.ShowSnapshot([], [], new ConnectPanelInfo { VIState = VIState.Idle, IsReentrant = true });
        Assert.Equal("Yes", model.ReentrantStateText);
        model.MarkNotChecked("目标端口已变化");
        Assert.Equal("未确认", model.ReentrantStateText);
    }
}
