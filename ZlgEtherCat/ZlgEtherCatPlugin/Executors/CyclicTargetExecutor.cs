using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class CyclicTargetExecutor : ZlgExecutorBase<CyclicTargetSetting>
{
    public CyclicTargetExecutor() : base(new CyclicTargetPlugin()) { }
}
