using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StartCaptureExecutor : ZlgExecutorBase<StartCaptureSetting>
{
    public StartCaptureExecutor() : base(new StartCapturePlugin()) { }
}
