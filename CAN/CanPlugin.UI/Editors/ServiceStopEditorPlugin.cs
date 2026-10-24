using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class ServiceStopEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new ServiceStopPlugin();
}
