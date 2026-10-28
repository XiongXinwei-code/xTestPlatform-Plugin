using System.Reflection;
using Xunit;

namespace OpcUa.Tests;

public class SaveReentrancyTests
{
    [StaFact]
    public void FlushDoesNotInvokeTheSamePendingSaveAgainWhenPublishingReentersRefresh()
    {
        var type = typeof(OpcUa.UI.ViewModels.OpcUaConnectViewModel).Assembly.GetType("OpcUa.UI.ViewModels.EditorSaveCoordinator")!;
        object coordinator = Activator.CreateInstance(type)!;
        int calls = 0;
        Action save = () => { if (++calls == 1) type.GetMethod("Flush")!.Invoke(coordinator, null); };
        type.GetMethod("Queue")!.Invoke(coordinator, [save]);
        type.GetMethod("Flush")!.Invoke(coordinator, null);
        Assert.Equal(1, calls);
    }
}
