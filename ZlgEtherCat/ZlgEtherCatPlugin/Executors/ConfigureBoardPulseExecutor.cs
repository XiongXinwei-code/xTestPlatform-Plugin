using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ConfigureBoardPulseExecutor : ZlgExecutorBase<ConfigureBoardPulseSetting>
{
    public ConfigureBoardPulseExecutor() : base(new ConfigureBoardPulsePlugin()) { }
}
