using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class FeedWatchdogExecutor : ZlgExecutorBase<FeedWatchdogSetting>
{
    public FeedWatchdogExecutor() : base(new FeedWatchdogPlugin()) { }
}
