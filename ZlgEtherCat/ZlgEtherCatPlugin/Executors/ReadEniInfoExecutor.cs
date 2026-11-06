using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadEniInfoExecutor : ZlgExecutorBase<ReadEniInfoSetting>
{
    public ReadEniInfoExecutor() : base(new ReadEniInfoPlugin()) { }
}
