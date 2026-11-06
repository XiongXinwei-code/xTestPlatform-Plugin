using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StartBoardPwmExecutor : ZlgExecutorBase<StartBoardPwmSetting>
{
    public StartBoardPwmExecutor() : base(new StartBoardPwmPlugin()) { }
}
