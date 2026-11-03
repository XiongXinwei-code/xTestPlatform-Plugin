using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcConnectPlugin : NiGrpcPluginBase<NiGrpcConnectSetting>
{
    public NiGrpcConnectPlugin() : base(Operation.Connect, null, null, "连接并检查 gRPC 服务器健康状态") { }
    public override string StepTypeId => "NiGrpc.Connect";
    public override string DisplayName => "NiGrpc_Connection_Connect";
}
