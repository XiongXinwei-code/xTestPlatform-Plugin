using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class EnumerateSlavesExecutor : ZlgExecutorBase<EnumerateSlavesSetting>
{
    public EnumerateSlavesExecutor() : base(new EnumerateSlavesPlugin()) { }
}
