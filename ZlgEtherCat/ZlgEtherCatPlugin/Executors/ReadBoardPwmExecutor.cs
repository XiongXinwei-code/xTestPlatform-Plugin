using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadBoardPwmExecutor : ZlgExecutorBase<ReadBoardPwmSetting>
{
    public ReadBoardPwmExecutor() : base(new ReadBoardPwmPlugin()) { }
}
