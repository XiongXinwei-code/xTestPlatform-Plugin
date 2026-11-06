using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ResetAxisFaultExecutor : ZlgExecutorBase<ResetAxisFaultSetting>
{
    public ResetAxisFaultExecutor() : base(new ResetAxisFaultPlugin()) { }
}
