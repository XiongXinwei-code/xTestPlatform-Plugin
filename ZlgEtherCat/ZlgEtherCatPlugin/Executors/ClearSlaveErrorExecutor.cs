using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ClearSlaveErrorExecutor : ZlgExecutorBase<ClearSlaveErrorSetting>
{
    public ClearSlaveErrorExecutor() : base(new ClearSlaveErrorPlugin()) { }
}
