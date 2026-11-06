using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class MovePositionExecutor : ZlgExecutorBase<MovePositionSetting>
{
    public MovePositionExecutor() : base(new MovePositionPlugin()) { }
}
