using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class RunVelocityExecutor : ZlgExecutorBase<RunVelocitySetting>
{
    public RunVelocityExecutor() : base(new RunVelocityPlugin()) { }
}
