using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadBoardPulseExecutor : ZlgExecutorBase<ReadBoardPulseSetting>
{
    public ReadBoardPulseExecutor() : base(new ReadBoardPulsePlugin()) { }
}
