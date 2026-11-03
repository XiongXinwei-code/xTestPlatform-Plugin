using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetWriteCanPlugin : NiGrpcPluginBase<NiGrpcXnetWriteCanSetting>
{
    public NiGrpcXnetWriteCanPlugin() : base(Operation.Invoke, NiDriver.Xnet, "WriteFrame", "发送 XNET CAN 帧") { }
    public override string StepTypeId => "NiGrpc.XnetWriteCan";
    public override string DisplayName => "NiGrpc_XNET_ReadWrite_WriteCanFrames";
}
