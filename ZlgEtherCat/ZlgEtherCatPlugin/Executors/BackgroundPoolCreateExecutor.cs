using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BackgroundPoolCreateExecutor : ZlgExecutorBase<BackgroundPoolCreateSetting>
{
    public BackgroundPoolCreateExecutor() : base(new BackgroundPoolCreatePlugin()) { }
}
