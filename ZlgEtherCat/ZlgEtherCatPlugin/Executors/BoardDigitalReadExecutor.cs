using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BoardDigitalReadExecutor : ZlgExecutorBase<BoardDigitalReadSetting>
{
    public BoardDigitalReadExecutor() : base(new BoardDigitalReadPlugin()) { }
}
