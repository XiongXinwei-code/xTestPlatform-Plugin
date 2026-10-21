using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class ReadModeSetting : AxisSetting
{
    [VariablePathField]
    public string ModeVariable { get; set; } = "";
}
