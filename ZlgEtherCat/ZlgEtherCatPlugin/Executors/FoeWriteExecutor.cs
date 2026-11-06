using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class FoeWriteExecutor : ZlgExecutorBase<FoeWriteSetting>
{
    public FoeWriteExecutor() : base(new FoeWritePlugin()) { }
}
