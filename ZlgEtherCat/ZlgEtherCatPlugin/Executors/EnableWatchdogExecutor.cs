using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class EnableWatchdogExecutor : ZlgExecutorBase<EnableWatchdogSetting>
{
    public EnableWatchdogExecutor() : base(new EnableWatchdogPlugin()) { }
}
