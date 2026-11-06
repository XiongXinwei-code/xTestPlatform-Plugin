using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolCreateExecutor : ZlgExecutorBase<PeriodicPoolCreateSetting>
{
    public PeriodicPoolCreateExecutor() : base(new PeriodicPoolCreatePlugin()) { }
}
