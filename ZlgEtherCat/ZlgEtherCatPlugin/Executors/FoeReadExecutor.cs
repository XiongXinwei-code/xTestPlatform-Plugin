using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class FoeReadExecutor : ZlgExecutorBase<FoeReadSetting>
{
    public FoeReadExecutor() : base(new FoeReadPlugin()) { }
}
