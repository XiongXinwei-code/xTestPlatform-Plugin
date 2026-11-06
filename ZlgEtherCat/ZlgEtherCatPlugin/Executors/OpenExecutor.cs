using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class OpenExecutor : ZlgExecutorBase<OpenSetting>
{
    public OpenExecutor() : base(new OpenPlugin()) { }
}
