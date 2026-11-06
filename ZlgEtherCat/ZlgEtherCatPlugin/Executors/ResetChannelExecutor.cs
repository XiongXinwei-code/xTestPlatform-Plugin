using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ResetChannelExecutor : ZlgExecutorBase<ResetChannelSetting>
{
    public ResetChannelExecutor() : base(new ResetChannelPlugin()) { }
}
