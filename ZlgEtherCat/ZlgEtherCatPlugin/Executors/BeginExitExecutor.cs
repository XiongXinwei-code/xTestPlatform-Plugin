using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BeginExitExecutor : ZlgExecutorBase<BeginExitSetting>
{
    public BeginExitExecutor() : base(new BeginExitPlugin()) { }
}
