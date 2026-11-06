using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class SetAxisModeExecutor : ZlgExecutorBase<SetAxisModeSetting>
{
    public SetAxisModeExecutor() : base(new SetAxisModePlugin()) { }
}
