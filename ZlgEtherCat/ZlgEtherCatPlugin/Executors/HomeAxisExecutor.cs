using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class HomeAxisExecutor : ZlgExecutorBase<HomeAxisSetting>
{
    public HomeAxisExecutor() : base(new HomeAxisPlugin()) { }
}
