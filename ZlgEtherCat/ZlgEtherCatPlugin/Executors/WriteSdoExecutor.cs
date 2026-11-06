using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteSdoExecutor : ZlgExecutorBase<WriteSdoSetting>
{
    public WriteSdoExecutor() : base(new WriteSdoPlugin()) { }
}
