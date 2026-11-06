using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class CheckEniExecutor : ZlgExecutorBase<CheckEniSetting>
{
    public CheckEniExecutor() : base(new CheckEniPlugin()) { }
}
