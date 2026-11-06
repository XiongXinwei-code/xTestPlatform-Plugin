using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PdoReadExecutor : ZlgExecutorBase<PdoReadSetting>
{
    public PdoReadExecutor() : base(new PdoReadPlugin()) { }
}
