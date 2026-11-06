using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadSdoExecutor : ZlgExecutorBase<ReadSdoSetting>
{
    public ReadSdoExecutor() : base(new ReadSdoPlugin()) { }
}
