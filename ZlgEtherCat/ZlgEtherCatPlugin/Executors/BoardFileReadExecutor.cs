using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BoardFileReadExecutor : ZlgExecutorBase<BoardFileReadSetting>
{
    public BoardFileReadExecutor() : base(new BoardFileReadPlugin()) { }
}
