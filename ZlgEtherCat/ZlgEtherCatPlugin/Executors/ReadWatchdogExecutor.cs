using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadWatchdogExecutor : ZlgExecutorBase<ReadWatchdogSetting>
{
    public ReadWatchdogExecutor() : base(new ReadWatchdogPlugin()) { }
}
