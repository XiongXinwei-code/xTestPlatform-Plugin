using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteSlaveMemoryExecutor : ZlgExecutorBase<WriteSlaveMemorySetting>
{
    public WriteSlaveMemoryExecutor() : base(new WriteSlaveMemoryPlugin()) { }
}
