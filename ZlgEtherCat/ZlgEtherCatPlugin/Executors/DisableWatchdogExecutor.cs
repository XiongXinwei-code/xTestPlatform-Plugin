using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class DisableWatchdogExecutor : ZlgExecutorBase<DisableWatchdogSetting>
{
    public DisableWatchdogExecutor() : base(new DisableWatchdogPlugin()) { }
}
