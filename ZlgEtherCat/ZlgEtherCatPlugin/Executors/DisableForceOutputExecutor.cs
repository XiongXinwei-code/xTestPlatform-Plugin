using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class DisableForceOutputExecutor : ZlgExecutorBase<DisableForceOutputSetting>
{
    public DisableForceOutputExecutor() : base(new DisableForceOutputPlugin()) { }
}
