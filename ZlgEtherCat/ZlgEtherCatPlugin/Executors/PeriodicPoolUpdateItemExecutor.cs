using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolUpdateItemExecutor : ZlgExecutorBase<PeriodicPoolUpdateItemSetting>
{
    public PeriodicPoolUpdateItemExecutor() : base(new PeriodicPoolUpdateItemPlugin()) { }
}
