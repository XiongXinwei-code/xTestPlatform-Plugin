using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StartBoardLatchExecutor : ZlgExecutorBase<StartBoardLatchSetting>
{
    public StartBoardLatchExecutor() : base(new StartBoardLatchPlugin()) { }
}
