using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class AxisReadExecutor : ZlgExecutorBase<AxisReadSetting>
{
    public AxisReadExecutor() : base(new AxisReadPlugin()) { }
}
