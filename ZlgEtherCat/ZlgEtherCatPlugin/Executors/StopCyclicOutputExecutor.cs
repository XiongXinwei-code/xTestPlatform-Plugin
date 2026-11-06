using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopCyclicOutputExecutor : ZlgExecutorBase<StopCyclicOutputSetting>
{
    public StopCyclicOutputExecutor() : base(new StopCyclicOutputPlugin()) { }
}
