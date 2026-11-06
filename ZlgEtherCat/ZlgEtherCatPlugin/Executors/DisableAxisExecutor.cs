using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class DisableAxisExecutor : ZlgExecutorBase<DisableAxisSetting>
{
    public DisableAxisExecutor() : base(new DisableAxisPlugin()) { }
}
