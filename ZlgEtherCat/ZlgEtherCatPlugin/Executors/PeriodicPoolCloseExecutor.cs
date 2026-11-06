using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolCloseExecutor : ZlgExecutorBase<PeriodicPoolCloseSetting>
{
    public PeriodicPoolCloseExecutor() : base(new PeriodicPoolClosePlugin()) { }
}
