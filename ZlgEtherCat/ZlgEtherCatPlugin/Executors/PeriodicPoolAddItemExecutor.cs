using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolAddItemExecutor : ZlgExecutorBase<PeriodicPoolAddItemSetting>
{
    public PeriodicPoolAddItemExecutor() : base(new PeriodicPoolAddItemPlugin()) { }
}
