using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadStateSetting : AxisSetting
{
    [VariablePathField]
    public string StatusWordVariable { get; set; } = "";
    [VariablePathField]
    public string StateVariable { get; set; } = "";
}
