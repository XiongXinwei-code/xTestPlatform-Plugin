using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolStopItemExecutor : ZlgExecutorBase<PeriodicPoolStopItemSetting>
{
    public PeriodicPoolStopItemExecutor() : base(new PeriodicPoolStopItemPlugin()) { }
}
