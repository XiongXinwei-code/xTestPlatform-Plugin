using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class OutputBoardPulseExecutor : ZlgExecutorBase<OutputBoardPulseSetting>
{
    public OutputBoardPulseExecutor() : base(new OutputBoardPulsePlugin()) { }
}
