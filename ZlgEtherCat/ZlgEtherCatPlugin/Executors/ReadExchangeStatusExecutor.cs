using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadExchangeStatusExecutor : ZlgExecutorBase<ReadExchangeStatusSetting>
{
    public ReadExchangeStatusExecutor() : base(new ReadExchangeStatusPlugin()) { }
}
