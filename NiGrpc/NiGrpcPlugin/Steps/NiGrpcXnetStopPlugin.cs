using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetStopPlugin : NiGrpcPluginBase<NiGrpcXnetStopSetting>
{
    public NiGrpcXnetStopPlugin() : base(Operation.Invoke, NiDriver.Xnet, "Stop", "停止 XNET 会话") { }
    public override string StepTypeId => "NiGrpc.XnetStop";
    public override string DisplayName => "NiGrpc_XNET_TasksSessions_StopSession";
}
