using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class RunTorqueExecutor : ZlgExecutorBase<RunTorqueSetting>
{
    public RunTorqueExecutor() : base(new RunTorquePlugin()) { }
}
