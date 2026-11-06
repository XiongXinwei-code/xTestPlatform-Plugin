using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BackgroundPoolStatusExecutor : ZlgExecutorBase<BackgroundPoolStatusSetting>
{
    public BackgroundPoolStatusExecutor() : base(new BackgroundPoolStatusPlugin()) { }
}
