using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BackgroundPoolCloseExecutor : ZlgExecutorBase<BackgroundPoolCloseSetting>
{
    public BackgroundPoolCloseExecutor() : base(new BackgroundPoolClosePlugin()) { }
}
