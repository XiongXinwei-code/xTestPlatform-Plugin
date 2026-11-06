using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class BackgroundPoolUpdateItemExecutor : ZlgExecutorBase<BackgroundPoolUpdateItemSetting>
{
    public BackgroundPoolUpdateItemExecutor() : base(new BackgroundPoolUpdateItemPlugin()) { }
}
