using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadPiRangeExecutor : ZlgExecutorBase<ReadPiRangeSetting>
{
    public ReadPiRangeExecutor() : base(new ReadPiRangePlugin()) { }
}
