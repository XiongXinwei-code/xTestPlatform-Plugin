using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class DisablePiExecutor : ZlgExecutorBase<DisablePiSetting>
{
    public DisablePiExecutor() : base(new DisablePiPlugin()) { }
}
