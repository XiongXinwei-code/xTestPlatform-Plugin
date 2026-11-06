using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class EnableAxisExecutor : ZlgExecutorBase<EnableAxisSetting>
{
    public EnableAxisExecutor() : base(new EnableAxisPlugin()) { }
}
