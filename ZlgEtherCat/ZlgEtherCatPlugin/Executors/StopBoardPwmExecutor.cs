using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopBoardPwmExecutor : ZlgExecutorBase<StopBoardPwmSetting>
{
    public StopBoardPwmExecutor() : base(new StopBoardPwmPlugin()) { }
}
