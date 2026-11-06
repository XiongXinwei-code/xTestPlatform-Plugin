using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class EnumerateBoardsExecutor : ZlgExecutorBase<EnumerateBoardsSetting>
{
    public EnumerateBoardsExecutor() : base(new EnumerateBoardsPlugin()) { }
}
