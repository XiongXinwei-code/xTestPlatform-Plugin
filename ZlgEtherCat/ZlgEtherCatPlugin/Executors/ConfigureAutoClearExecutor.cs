using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureAutoClearExecutor : ZlgExecutorBase<ConfigureAutoClearSetting>
{
    public ConfigureAutoClearExecutor() : base(new ConfigureAutoClearPlugin()) { }
}
