using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadHistoryExecutor : ZlgExecutorBase<ReadHistorySetting>
{
    public ReadHistoryExecutor() : base(new ReadHistoryPlugin()) { }
}
