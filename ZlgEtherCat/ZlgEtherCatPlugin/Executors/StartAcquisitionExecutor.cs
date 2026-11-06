using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StartAcquisitionExecutor : ZlgExecutorBase<StartAcquisitionSetting>
{
    public StartAcquisitionExecutor() : base(new StartAcquisitionPlugin()) { }
}
