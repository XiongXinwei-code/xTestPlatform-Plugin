using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopMotionExecutor : ZlgExecutorBase<StopMotionSetting>
{
    public StopMotionExecutor() : base(new StopMotionPlugin()) { }
}
