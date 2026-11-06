using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WaitNextPdoExecutor : ZlgExecutorBase<WaitNextPdoSetting>
{
    public WaitNextPdoExecutor() : base(new WaitNextPdoPlugin()) { }
}
