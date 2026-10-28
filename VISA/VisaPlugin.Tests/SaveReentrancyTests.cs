using Xunit;

namespace VISA.Tests;

public class SaveReentrancyTests
{
    [StaFact]
    public void FlushDoesNotInvokeTheSamePendingSaveAgainWhenPublishingReentersRefresh()
    {
        var type = typeof(VISA.UI.ViewModels.VisaOpenViewModel).Assembly.GetType("VISA.UI.ViewModels.EditorSaveCoordinator")!;
        object coordinator = Activator.CreateInstance(type)!;
        int calls = 0;
        Action save = () => { if (++calls == 1) type.GetMethod("Flush")!.Invoke(coordinator, null); };
        type.GetMethod("Queue")!.Invoke(coordinator, [save]);
        type.GetMethod("Flush")!.Invoke(coordinator, null);
        Assert.Equal(1, calls);
    }
}
