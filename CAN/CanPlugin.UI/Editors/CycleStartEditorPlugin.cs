using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class CycleStartEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new CycleStartPlugin();
}
