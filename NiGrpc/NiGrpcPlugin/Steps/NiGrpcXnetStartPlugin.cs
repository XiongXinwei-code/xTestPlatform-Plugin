using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetStartPlugin : NiGrpcPluginBase<NiGrpcXnetStartSetting>
{
    public NiGrpcXnetStartPlugin() : base(Operation.Invoke, NiDriver.Xnet, "Start", "启动 XNET 会话") { }
    public override string StepTypeId => "NiGrpc.XnetStart";
    public override string DisplayName => "NiGrpc_XNET_TasksSessions_StartSession";
}
