using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetReadStatePlugin : NiGrpcPluginBase<NiGrpcXnetReadStateSetting>
{
    public NiGrpcXnetReadStatePlugin() : base(Operation.Invoke, NiDriver.Xnet, "ReadState", "读取已支持的 XNET 状态") { }
    public override string StepTypeId => "NiGrpc.XnetReadState";
    public override string DisplayName => "NiGrpc_XNET_PropertiesState_ReadState";
}
