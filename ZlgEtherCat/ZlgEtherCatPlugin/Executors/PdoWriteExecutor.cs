using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class PdoWriteExecutor : ZlgExecutorBase<PdoWriteSetting>
{
    public PdoWriteExecutor() : base(new PdoWritePlugin()) { }
}
