using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class UnbindAxisExecutor : ZlgExecutorBase<UnbindAxisSetting>
{
    public UnbindAxisExecutor() : base(new UnbindAxisPlugin()) { }
}
