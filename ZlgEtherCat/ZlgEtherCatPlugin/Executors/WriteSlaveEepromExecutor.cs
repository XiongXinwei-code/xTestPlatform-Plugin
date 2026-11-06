using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteSlaveEepromExecutor : ZlgExecutorBase<WriteSlaveEepromSetting>
{
    public WriteSlaveEepromExecutor() : base(new WriteSlaveEepromPlugin()) { }
}
