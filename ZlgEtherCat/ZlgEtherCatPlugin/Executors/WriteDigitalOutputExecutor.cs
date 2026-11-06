using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteDigitalOutputExecutor : ZlgExecutorBase<WriteDigitalOutputSetting>
{
    public WriteDigitalOutputExecutor() : base(new WriteDigitalOutputPlugin()) { }
}
