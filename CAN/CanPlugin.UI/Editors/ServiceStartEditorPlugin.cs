using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class ServiceStartEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new ServiceStartPlugin();
}
