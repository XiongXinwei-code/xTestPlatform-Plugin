using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopBusExecutor : ZlgExecutorBase<StopBusSetting>
{
    public StopBusExecutor() : base(new StopBusPlugin()) { }
}
