using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDisconnectPlugin : NiGrpcPluginBase<NiGrpcDisconnectSetting>
{
    public NiGrpcDisconnectPlugin() : base(Operation.Disconnect, null, null, "关闭连接及其所属会话") { }
    public override string StepTypeId => "NiGrpc.Disconnect";
    public override string DisplayName => "NiGrpc_Connection_Disconnect";
}
