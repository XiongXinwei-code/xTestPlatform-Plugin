using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BoardUpdateExecutor : ZlgExecutorBase<BoardUpdateSetting>
{
    public BoardUpdateExecutor() : base(new BoardUpdatePlugin()) { }
}
