using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ResetBoardExecutor : ZlgExecutorBase<ResetBoardSetting>
{
    public ResetBoardExecutor() : base(new ResetBoardPlugin()) { }
}
