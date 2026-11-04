using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcCloseSessionPlugin : NiGrpcPluginBase<NiGrpcCloseSessionSetting>
{
    public NiGrpcCloseSessionPlugin() : base(Operation.CloseSession, null, null, "关闭指定远端会话") { }
    public override string StepTypeId => "NiGrpc.CloseSession";
    public override string DisplayName => "NiGrpc_Connection_CloseSession";
}
