using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class StopCaptureExecutor : ZlgExecutorBase<StopCaptureSetting>
{
    public StopCaptureExecutor() : base(new StopCapturePlugin()) { }
}
