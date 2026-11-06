using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class SlaveDiagnosticsExecutor : ZlgExecutorBase<SlaveDiagnosticsSetting>
{
    public SlaveDiagnosticsExecutor() : base(new SlaveDiagnosticsPlugin()) { }
}
