using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcXnetSetPropertyPlugin : NiGrpcPluginBase<NiGrpcXnetSetPropertySetting>
{
    public NiGrpcXnetSetPropertyPlugin() : base(Operation.Invoke, NiDriver.Xnet, "SetProperty", "设置已支持的 XNET 属性") { }
    public override string StepTypeId => "NiGrpc.XnetSetProperty";
    public override string DisplayName => "NiGrpc_XNET_PropertiesState_SetProperty";
}
