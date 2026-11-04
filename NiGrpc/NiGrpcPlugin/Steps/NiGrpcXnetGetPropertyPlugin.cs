using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetGetPropertyPlugin : NiGrpcPluginBase<NiGrpcXnetGetPropertySetting>
{
    public NiGrpcXnetGetPropertyPlugin() : base(Operation.Invoke, NiDriver.Xnet, "GetProperty", "读取已支持的 XNET 属性") { }
    public override string StepTypeId => "NiGrpc.XnetGetProperty";
    public override string DisplayName => "NiGrpc_XNET_PropertiesState_GetProperty";
}
