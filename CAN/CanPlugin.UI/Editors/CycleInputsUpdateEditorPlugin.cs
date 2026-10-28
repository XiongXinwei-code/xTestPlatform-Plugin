using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class CycleInputsUpdateEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new CycleInputsUpdatePlugin();
}
