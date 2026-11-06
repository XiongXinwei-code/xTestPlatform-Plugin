using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureReinitExecutor : ZlgExecutorBase<ConfigureReinitSetting>
{
    public ConfigureReinitExecutor() : base(new ConfigureReinitPlugin()) { }
}
