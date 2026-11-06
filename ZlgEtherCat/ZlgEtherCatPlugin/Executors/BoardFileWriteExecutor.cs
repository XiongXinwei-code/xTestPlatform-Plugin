using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BoardFileWriteExecutor : ZlgExecutorBase<BoardFileWriteSetting>
{
    public BoardFileWriteExecutor() : base(new BoardFileWritePlugin()) { }
}
