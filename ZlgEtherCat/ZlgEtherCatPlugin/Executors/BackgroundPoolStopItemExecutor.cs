using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BackgroundPoolStopItemExecutor : ZlgExecutorBase<BackgroundPoolStopItemSetting>
{
    public BackgroundPoolStopItemExecutor() : base(new BackgroundPoolStopItemPlugin()) { }
}
