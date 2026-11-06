using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BindAxisExecutor : ZlgExecutorBase<BindAxisSetting>
{
    public BindAxisExecutor() : base(new BindAxisPlugin()) { }
}
