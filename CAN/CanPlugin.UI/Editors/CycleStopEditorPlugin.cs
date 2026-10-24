using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class CycleStopEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new CycleStopPlugin();
}
