using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteBoardDoExecutor : ZlgExecutorBase<WriteBoardDoSetting>
{
    public WriteBoardDoExecutor() : base(new WriteBoardDoPlugin()) { }
}
