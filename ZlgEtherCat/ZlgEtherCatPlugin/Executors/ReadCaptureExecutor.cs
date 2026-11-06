using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ReadCaptureExecutor : ZlgExecutorBase<ReadCaptureSetting>
{
    public ReadCaptureExecutor() : base(new ReadCapturePlugin()) { }
}
