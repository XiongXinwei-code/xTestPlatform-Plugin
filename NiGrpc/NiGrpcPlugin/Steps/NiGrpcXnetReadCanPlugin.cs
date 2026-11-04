using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetReadCanPlugin : NiGrpcPluginBase<NiGrpcXnetReadCanSetting>
{
    public NiGrpcXnetReadCanPlugin() : base(Operation.Invoke, NiDriver.Xnet, "ReadFrame", "读取 XNET CAN 帧") { }
    public override string StepTypeId => "NiGrpc.XnetReadCan";
    public override string DisplayName => "NiGrpc_XNET_ReadWrite_ReadCanFrames";
}
