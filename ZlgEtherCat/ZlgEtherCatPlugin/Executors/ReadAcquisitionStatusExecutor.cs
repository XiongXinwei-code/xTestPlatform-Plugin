using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadAcquisitionStatusExecutor : ZlgExecutorBase<ReadAcquisitionStatusSetting>
{
    public ReadAcquisitionStatusExecutor() : base(new ReadAcquisitionStatusPlugin()) { }
}
