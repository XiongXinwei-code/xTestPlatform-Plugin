using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadSlaveEepromExecutor : ZlgExecutorBase<ReadSlaveEepromSetting>
{
    public ReadSlaveEepromExecutor() : base(new ReadSlaveEepromPlugin()) { }
}
