using CAN.CANopen.Cyclic;

namespace CAN.UI;

public sealed class SnapshotReadEditorPlugin : CyclicServiceEditorBase
{
    protected override CyclicPluginBase Plugin => new SnapshotReadPlugin();
}
