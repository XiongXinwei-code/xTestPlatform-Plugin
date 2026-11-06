using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PeriodicPoolStatusExecutor : ZlgExecutorBase<PeriodicPoolStatusSetting>
{
    public PeriodicPoolStatusExecutor() : base(new PeriodicPoolStatusPlugin()) { }
}
