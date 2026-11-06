using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StartCyclicOutputExecutor : ZlgExecutorBase<StartCyclicOutputSetting>
{
    public StartCyclicOutputExecutor() : base(new StartCyclicOutputPlugin()) { }
}
