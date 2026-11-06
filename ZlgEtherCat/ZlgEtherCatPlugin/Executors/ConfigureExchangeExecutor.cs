using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureExchangeExecutor : ZlgExecutorBase<ConfigureExchangeSetting>
{
    public ConfigureExchangeExecutor() : base(new ConfigureExchangePlugin()) { }
}
