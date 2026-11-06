using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadSlaveMemoryExecutor : ZlgExecutorBase<ReadSlaveMemorySetting>
{
    public ReadSlaveMemoryExecutor() : base(new ReadSlaveMemoryPlugin()) { }
}
