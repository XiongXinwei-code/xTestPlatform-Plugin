using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureForceOutputExecutor : ZlgExecutorBase<ConfigureForceOutputSetting>
{
    public ConfigureForceOutputExecutor() : base(new ConfigureForceOutputPlugin()) { }
}
