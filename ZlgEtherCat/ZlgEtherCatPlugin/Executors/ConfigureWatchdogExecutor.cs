using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureWatchdogExecutor : ZlgExecutorBase<ConfigureWatchdogSetting>
{
    public ConfigureWatchdogExecutor() : base(new ConfigureWatchdogPlugin()) { }
}
