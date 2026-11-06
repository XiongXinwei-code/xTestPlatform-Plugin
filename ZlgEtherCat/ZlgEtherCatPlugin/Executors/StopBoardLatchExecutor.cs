using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopBoardLatchExecutor : ZlgExecutorBase<StopBoardLatchSetting>
{
    public StopBoardLatchExecutor() : base(new StopBoardLatchPlugin()) { }
}
