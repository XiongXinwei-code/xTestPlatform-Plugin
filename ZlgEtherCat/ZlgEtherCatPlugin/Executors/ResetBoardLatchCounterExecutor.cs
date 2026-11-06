using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ResetBoardLatchCounterExecutor : ZlgExecutorBase<ResetBoardLatchCounterSetting>
{
    public ResetBoardLatchCounterExecutor() : base(new ResetBoardLatchCounterPlugin()) { }
}
