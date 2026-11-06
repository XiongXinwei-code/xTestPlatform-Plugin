using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadDigitalInputExecutor : ZlgExecutorBase<ReadDigitalInputSetting>
{
    public ReadDigitalInputExecutor() : base(new ReadDigitalInputPlugin()) { }
}
