using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadForceOutputExecutor : ZlgExecutorBase<ReadForceOutputSetting>
{
    public ReadForceOutputExecutor() : base(new ReadForceOutputPlugin()) { }
}
