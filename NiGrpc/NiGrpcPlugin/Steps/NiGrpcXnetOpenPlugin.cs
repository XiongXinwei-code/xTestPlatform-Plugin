using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetOpenPlugin : NiGrpcPluginBase<NiGrpcXnetOpenSetting>
{
    public NiGrpcXnetOpenPlugin() : base(Operation.OpenSession, NiDriver.Xnet, null, "创建独占 XNET 会话") { }
    public override string StepTypeId => "NiGrpc.XnetOpen";
    public override string DisplayName => "NiGrpc_XNET_TasksSessions_CreateSession";
}
