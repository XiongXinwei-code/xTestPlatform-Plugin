using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class WriteAnalogOutputExecutor : ZlgExecutorBase<WriteAnalogOutputSetting>
{
    public WriteAnalogOutputExecutor() : base(new WriteAnalogOutputPlugin()) { }
}
