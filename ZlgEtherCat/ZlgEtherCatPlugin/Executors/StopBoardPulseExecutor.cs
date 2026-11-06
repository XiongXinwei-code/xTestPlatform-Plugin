using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopBoardPulseExecutor : ZlgExecutorBase<StopBoardPulseSetting>
{
    public StopBoardPulseExecutor() : base(new StopBoardPulsePlugin()) { }
}
