using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureBoardLatchExecutor : ZlgExecutorBase<ConfigureBoardLatchSetting>
{
    public ConfigureBoardLatchExecutor() : base(new ConfigureBoardLatchPlugin()) { }
}
