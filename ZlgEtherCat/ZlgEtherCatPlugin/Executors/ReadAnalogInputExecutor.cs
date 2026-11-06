using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadAnalogInputExecutor : ZlgExecutorBase<ReadAnalogInputSetting>
{
    public ReadAnalogInputExecutor() : base(new ReadAnalogInputPlugin()) { }
}
