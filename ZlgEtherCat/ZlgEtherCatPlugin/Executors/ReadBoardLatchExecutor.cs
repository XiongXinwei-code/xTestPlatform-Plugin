using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadBoardLatchExecutor : ZlgExecutorBase<ReadBoardLatchSetting>
{
    public ReadBoardLatchExecutor() : base(new ReadBoardLatchPlugin()) { }
}
